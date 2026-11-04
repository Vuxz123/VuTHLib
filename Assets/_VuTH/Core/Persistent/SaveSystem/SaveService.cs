#nullable enable
using System;
using System.IO;
using System.Threading;
using _VuTH.Common.Log;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using _VuTH.Core.Persistent.SaveSystem.Events;
using _VuTH.Core.Persistent.SaveSystem.Migrate;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// Save pipeline. Save: Serialize -> wrap with schema version -> Encrypt -> write.
    /// Load: read -> Decrypt -> unwrap -> Migrate -> Deserialize.
    /// <para>
    /// Loading never silently replaces stored data: unreadable data is reported (<see cref="SaveLoadStatus"/>), and
    /// corrupted data is copied to <c>key + </c><see cref="CorruptedBackupSuffix"/> before the caller may overwrite it.
    /// </para>
    /// </summary>
    public class SaveService : ISaveService, ISyncSaveService
    {
        /// <summary>Schema version used when none is configured.</summary>
        public const int DefaultSchemaVersion = 1;

        /// <summary>Suffix of the key that receives a raw copy of corrupted data.</summary>
        public const string CorruptedBackupSuffix = ".corrupt";

        private readonly ISaveBackend _backend;
        private readonly ISerializer _serializer;
        private readonly IEncryptor _encryptor;
        private readonly int _currentSchemaVersion;
        private readonly SaveMigrationChain _migrationChain;
        private readonly ISaveEventPublisher _eventPublisher;

        public SaveService(
            ISaveBackend backend,
            ISerializer serializer,
            IEncryptor encryptor,
            int currentSchemaVersion = DefaultSchemaVersion,
            SaveMigrationChain? migrationChain = null,
            ISaveEventPublisher? eventPublisher = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            if (currentSchemaVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(currentSchemaVersion), "Schema version cannot be negative.");

            _currentSchemaVersion = currentSchemaVersion;
            _eventPublisher = eventPublisher ?? new NullSaveEventPublisher();
            _migrationChain = migrationChain ?? new SaveMigrationChain();
        }

        public int SchemaVersion => _currentSchemaVersion;

        #region Save

        /// <inheritdoc />
        public bool TrySaveImmediate<T>(string key, T data)
        {
            if (_backend is not ISyncSaveBackend syncBackend)
                return false;

            try
            {
                syncBackend.SaveRaw(key, Encode(data));
            }
            catch (Exception ex)
            {
                Publish(p => p.OnSaveFailed(key, typeof(T).Name, ex.Message));
                throw;
            }

            Publish(p => p.OnSaveSuccess(key, typeof(T).Name));
            return true;
        }

        public async UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
        {
            try
            {
                await _backend.SaveRawAsync(key, Encode(data), cancellationToken);
            }
            catch (Exception ex)
            {
                Publish(p => p.OnSaveFailed(key, typeof(T).Name, ex.Message));
                throw;
            }

            // Outside the try: a throwing subscriber must not turn a completed save into a reported failure.
            Publish(p => p.OnSaveSuccess(key, typeof(T).Name));
        }

        private string Encode<T>(T data)
        {
            var wrapper = new SavePayloadWrapper(_currentSchemaVersion, _serializer.Serialize(data));
            return _encryptor.Encrypt(_serializer.Serialize(wrapper));
        }

        #endregion

        #region Load

        /// <inheritdoc />
        public async UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var result = await LoadCoreAsync<T>(key, cancellationToken);

            if (result.IsLoaded)
            {
                Publish(p => p.OnLoadSuccess(key, typeof(T).Name));
            }
            else
            {
                if (result.Status != SaveLoadStatus.NotFound)
                {
                    this.LogError($"Could not load '{key}' ({typeof(T).Name}): {result}");
                }

                Publish(p => p.OnLoadFailed(key, typeof(T).Name,
                    result.Status == SaveLoadStatus.NotFound ? "Key not found" : result.ToString()));
            }

            return result;
        }

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            var result = await TryLoadAsync<T>(key, cancellationToken);
            return result.IsLoaded ? result.Data : defaultValue;
        }

        private async UniTask<SaveLoadResult<T>> LoadCoreAsync<T>(string key, CancellationToken cancellationToken)
        {
            // 1. Read. An I/O error leaves the stored data untouched: report it, never treat it as "not found".
            string? raw;
            try
            {
                raw = await _backend.LoadRawAsync(key, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return SaveLoadResult<T>.Failure(SaveLoadStatus.ReadFailed, ex.Message);
            }

            if (raw == null) return SaveLoadResult<T>.NotFound();

            // 2. Decrypt + unwrap. Failure here means the stored bytes are damaged (or the key changed).
            SavePayloadWrapper wrapper;
            try
            {
                wrapper = _serializer.Deserialize<SavePayloadWrapper>(_encryptor.Decrypt(raw));
                if (wrapper?.Payload == null) throw new InvalidDataException("The save has no payload.");
            }
            catch (Exception ex)
            {
                return await BackUpCorruptedAsync<T>(key, raw, ex, cancellationToken);
            }

            // 3. A newer schema: reading it would drop the fields this version does not know.
            if (wrapper.SchemaVersion > _currentSchemaVersion)
            {
                return SaveLoadResult<T>.Failure(SaveLoadStatus.UnsupportedVersion,
                    $"Saved with schema version {wrapper.SchemaVersion}, newer than the current {_currentSchemaVersion}.");
            }

            // 4. Migrate.
            var payload = wrapper.Payload;
            var migrated = wrapper.SchemaVersion < _currentSchemaVersion;
            if (migrated)
            {
                try
                {
                    payload = _migrationChain.Migrate(payload, wrapper.SchemaVersion, _currentSchemaVersion);
                }
                catch (Exception ex)
                {
                    return SaveLoadResult<T>.Failure(SaveLoadStatus.MigrationFailed, ex.Message);
                }
            }

            // 5. Deserialize. After a migration a failure points at the migrator, not at damaged data.
            try
            {
                return SaveLoadResult<T>.Success(_serializer.Deserialize<T>(payload));
            }
            catch (Exception ex)
            {
                if (migrated)
                {
                    return SaveLoadResult<T>.Failure(SaveLoadStatus.MigrationFailed,
                        $"Migrated data from schema version {wrapper.SchemaVersion} could not be read: {ex.Message}");
                }

                return await BackUpCorruptedAsync<T>(key, raw, ex, cancellationToken);
            }
        }

        private async UniTask<SaveLoadResult<T>> BackUpCorruptedAsync<T>(
            string key, string raw, Exception error, CancellationToken cancellationToken)
        {
            var backupKey = key + CorruptedBackupSuffix;
            try
            {
                await _backend.SaveRawAsync(backupKey, raw, cancellationToken);
            }
            catch (Exception backupError)
            {
                // Without a backup the damaged data is the only copy: keep it.
                return SaveLoadResult<T>.Failure(SaveLoadStatus.Corrupted,
                    $"{error.Message} (backup to '{backupKey}' failed: {backupError.Message})");
            }

            return SaveLoadResult<T>.Failure(SaveLoadStatus.Corrupted,
                $"{error.Message} (raw data backed up to '{backupKey}')", canOverwrite: true);
        }

        #endregion

        public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return _backend.ExistsAsync(key, cancellationToken);
        }

        public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            return _backend.DeleteAsync(key, cancellationToken);
        }

        private void Publish(Action<ISaveEventPublisher> publish)
        {
            try
            {
                publish(_eventPublisher);
            }
            catch (Exception ex)
            {
                // A subscriber failure must not change the outcome of the save / load.
                this.LogError($"A save event subscriber threw: {ex}");
            }
        }
    }
}
