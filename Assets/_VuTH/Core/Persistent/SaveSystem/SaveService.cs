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
    /// corrupted data is copied to <see cref="GetCorruptedBackupKey"/> before the caller may overwrite it.
    /// </para>
    /// <para>
    /// The schema version and migrators given to the constructor apply to every key. Data that changes format on its
    /// own uses the <see cref="IVersionedSaveService"/> overloads with its own <see cref="SaveSchema"/>.
    /// </para>
    /// </summary>
    public class SaveService : ISaveService, ISyncSaveService, IVersionedSaveService
    {
        /// <summary>Schema version used when none is configured.</summary>
        public const int DefaultSchemaVersion = 1;

        /// <summary>Historical backup suffix. New backups use a reserved namespace.</summary>
        [Obsolete("Use GetCorruptedBackupKey(key). The .corrupt suffix is no longer used for backups.")]
        public const string CorruptedBackupSuffix = ".corrupt";

        /// <summary>Reserved backend key prefix. Application keys starting with it are rejected by this service.</summary>
        public const string CorruptedBackupPrefix = "__VuTH_Internal_Backup__/";

        public static string GetCorruptedBackupKey(string key)
        {
            ValidateApplicationKey(key);
            return CorruptedBackupPrefix + key;
        }

        private static void ValidateApplicationKey(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.StartsWith(CorruptedBackupPrefix, StringComparison.Ordinal))
                throw new ArgumentException("The key uses the reserved backup namespace.", nameof(key));
        }

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

        /// <summary>Service-wide schema version, used for data saved without its own <see cref="SaveSchema"/>.</summary>
        public int SchemaVersion => _currentSchemaVersion;

        /// <summary>
        /// Saves at least this long (in characters) are encrypted / decrypted on a worker thread when the encryptor is
        /// an <see cref="IThreadSafeEncryptor"/>, so AES on large saves does not stall the frame. Serialization stays on
        /// the calling thread: the payload may reference live game state. Never used on WebGL (no threads).
        /// </summary>
        public int ThreadPoolEncryptionThreshold { get; set; } = 64 * 1024;

        #region Save

        /// <inheritdoc />
        public bool SupportsSyncSave => _backend is ISyncSaveBackend;

        /// <inheritdoc />
        public bool TrySaveImmediate<T>(string key, T data) => TrySaveImmediateCore(key, data, null);

        /// <inheritdoc />
        public bool TrySaveImmediate<T>(string key, T data, SaveSchema schema) =>
            TrySaveImmediateCore(key, data, schema ?? throw new ArgumentNullException(nameof(schema)));

        private bool TrySaveImmediateCore<T>(string key, T data, SaveSchema? schema)
        {
            ValidateApplicationKey(key);
            if (_backend is not ISyncSaveBackend syncBackend)
                return false;

            try
            {
                syncBackend.SaveRaw(key, _encryptor.Encrypt(Wrap(data, schema)));
            }
            catch (Exception ex)
            {
                Publish(p => p.OnSaveFailed(key, typeof(T).Name, ex.Message));
                throw;
            }

            Publish(p => p.OnSaveSuccess(key, typeof(T).Name));
            return true;
        }

        public UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default) =>
            SaveCoreAsync(key, data, null, cancellationToken);

        /// <inheritdoc />
        public UniTask SaveAsync<T>(string key, T data, SaveSchema schema, CancellationToken cancellationToken = default) =>
            SaveCoreAsync(key, data, schema ?? throw new ArgumentNullException(nameof(schema)), cancellationToken);

        private async UniTask SaveCoreAsync<T>(string key, T data, SaveSchema? schema, CancellationToken cancellationToken)
        {
            ValidateApplicationKey(key);
            try
            {
                // Serialized before any await: the save is a snapshot of the data at the time of the call.
                var ordered = _backend as IOrderedSaveBackend;
                var ticket = ordered?.ReserveWrite() ?? 0;
                var plain = Wrap(data, schema);
                var encrypted = await TransformAsync(plain, _encryptor.Encrypt, cancellationToken);
                if (ordered != null)
                    await ordered.SaveRawOrderedAsync(key, encrypted, ticket, cancellationToken);
                else
                    await _backend.SaveRawAsync(key, encrypted, cancellationToken);
            }
            catch (Exception ex)
            {
                Publish(p => p.OnSaveFailed(key, typeof(T).Name, ex.Message));
                throw;
            }

            // Outside the try: a throwing subscriber must not turn a completed save into a reported failure.
            Publish(p => p.OnSaveSuccess(key, typeof(T).Name));
        }

        private string Wrap<T>(T data, SaveSchema? schema)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            var wrapper = new SavePayloadWrapper(schema?.Version ?? _currentSchemaVersion, _serializer.Serialize(data));
            return _serializer.Serialize(wrapper);
        }

        /// <summary>Encrypts / decrypts; on a worker thread for large data when the encryptor allows it.</summary>
        private UniTask<string> TransformAsync(string text, Func<string, string> transform, CancellationToken cancellationToken)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (text.Length >= ThreadPoolEncryptionThreshold && EncryptorThreading.IsThreadSafe(_encryptor))
            {
                // Returns to the calling (main) thread afterwards.
                return UniTask.RunOnThreadPool(() => transform(text), cancellationToken: cancellationToken);
            }
#endif
            return UniTask.FromResult(transform(text));
        }

        #endregion

        #region Load

        /// <inheritdoc />
        public UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default) =>
            TryLoadCoreAsync<T>(key, null, cancellationToken);

        /// <inheritdoc />
        public UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, SaveSchema schema, CancellationToken cancellationToken = default) =>
            TryLoadCoreAsync<T>(key, schema ?? throw new ArgumentNullException(nameof(schema)), cancellationToken);

        private async UniTask<SaveLoadResult<T>> TryLoadCoreAsync<T>(string key, SaveSchema? schema, CancellationToken cancellationToken)
        {
            var result = await LoadCoreAsync<T>(key, schema, cancellationToken);

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

        private async UniTask<SaveLoadResult<T>> LoadCoreAsync<T>(string key, SaveSchema? schema, CancellationToken cancellationToken)
        {
            ValidateApplicationKey(key);
            var targetVersion = schema?.Version ?? _currentSchemaVersion;
            var migrationChain = schema?.Migrations ?? _migrationChain;

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
                var plain = await TransformAsync(raw, _encryptor.Decrypt, cancellationToken);
                wrapper = _serializer.Deserialize<SavePayloadWrapper>(plain);
                if (wrapper?.Payload == null) throw new InvalidDataException("The save has no payload.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return await BackUpCorruptedAsync<T>(key, raw, ex, cancellationToken);
            }

            // 3. A newer schema: reading it would drop the fields this version does not know.
            if (wrapper.SchemaVersion > targetVersion)
            {
                return SaveLoadResult<T>.Failure(SaveLoadStatus.UnsupportedVersion,
                    $"Saved with schema version {wrapper.SchemaVersion}, newer than the current {targetVersion}.");
            }

            // 4. Migrate.
            var payload = wrapper.Payload;
            var migrated = wrapper.SchemaVersion < targetVersion;
            if (migrated)
            {
                try
                {
                    payload = migrationChain.Migrate(payload, wrapper.SchemaVersion, targetVersion);
                }
                catch (Exception ex)
                {
                    return SaveLoadResult<T>.Failure(SaveLoadStatus.MigrationFailed, ex.Message);
                }
            }

            // 5. Deserialize. After a migration a failure points at the migrator, not at damaged data.
            try
            {
                var data = _serializer.Deserialize<T>(payload);
                if (data is null) throw new InvalidDataException("The save payload deserialized to null.");
                return SaveLoadResult<T>.Success(data);
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
            var backupKey = GetCorruptedBackupKey(key);
            try
            {
                await _backend.SaveRawAsync(backupKey, raw, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
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
            ValidateApplicationKey(key);
            return _backend.ExistsAsync(key, cancellationToken);
        }

        /// <summary>Deletes the data, and the backup of a corrupted version of it if there is one.</summary>
        public async UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            ValidateApplicationKey(key);
            await _backend.DeleteAsync(key, cancellationToken);
            await _backend.DeleteAsync(GetCorruptedBackupKey(key), cancellationToken);
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
