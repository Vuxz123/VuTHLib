#nullable enable
using System;
using System.Threading;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Events;
using _VuTH.Core.Persistant.SaveSystem.Migrate;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// Internal save service core logic - no ISaveService interface.
    /// Handles: Serialize -> Encrypt -> Save pipeline on save.
    /// Load: Load -> Decrypt -> Deserialize -> Migrate -> Return.
    /// </summary>
    internal class SaveService
    {
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
            int currentSchemaVersion = 1,
            ISaveEventPublisher? eventPublisher = null)
        {
            _backend = backend;
            _serializer = serializer;
            _encryptor = encryptor;
            _currentSchemaVersion = currentSchemaVersion;
            _eventPublisher = eventPublisher ?? new NullSaveEventPublisher();
            _migrationChain = new SaveMigrationChain(_serializer);
        }

        public async UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
        {
            try
            {
                // Step 1: Serialize data
                string serializedData = _serializer.Serialize(data);

                // Step 2: Create wrapper with schema version
                var wrapper = new SavePayloadWrapper(_currentSchemaVersion, serializedData);
                string wrapperJson = _serializer.Serialize(wrapper);

                // Step 3: Encrypt
                string encryptedData = _encryptor.Encrypt(wrapperJson);

                // Step 4: Save to backend
                await _backend.SaveRawAsync(key, encryptedData, cancellationToken);

                // Publish success event
                _eventPublisher.OnSaveSuccess(key, typeof(T).Name);
            }
            catch (Exception ex)
            {
                // Publish failure event
                _eventPublisher.OnSaveFailed(key, typeof(T).Name, ex.Message);
                throw;
            }
        }

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            try
            {
                // Step 1: Check existence
                bool exists = await _backend.Exists(key, cancellationToken);
                if (!exists)
                {
                    _eventPublisher.OnLoadFailed(key, typeof(T).Name, "Key not found");
                    return defaultValue;
                }

                // Step 2: Load raw data
                string? encryptedData = await _backend.LoadRawAsync(key, cancellationToken);
                if (encryptedData == null)
                {
                    _eventPublisher.OnLoadFailed(key, typeof(T).Name, "Load returned null");
                    return defaultValue;
                }

                // Step 3: Decrypt
                string wrapperJson = _encryptor.Decrypt(encryptedData);

                // Step 4: Deserialize wrapper
                var wrapper = _serializer.Deserialize<SavePayloadWrapper>(wrapperJson);

                // Step 5: Migrate if needed
                if (wrapper.SchemaVersion < _currentSchemaVersion)
                {
                    string migratedPayload = _migrationChain.Migrate(
                        wrapper.Payload,
                        wrapper.SchemaVersion,
                        _currentSchemaVersion);
                    wrapper.Payload = migratedPayload;
                    wrapper.SchemaVersion = _currentSchemaVersion;
                }

                // Step 6: Deserialize actual data
                T data = _serializer.Deserialize<T>(wrapper.Payload);

                // Publish success event
                _eventPublisher.OnLoadSuccess(key, typeof(T).Name);

                return data;
            }
            catch (Exception ex)
            {
                // Publish failure event
                _eventPublisher.OnLoadFailed(key, typeof(T).Name, ex.Message);
                return defaultValue;
            }
        }

        public async UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _backend.Exists(key, cancellationToken);
        }

        public async UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            await _backend.DeleteAsync(key, cancellationToken);
        }
    }
}
