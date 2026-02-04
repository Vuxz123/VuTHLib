#nullable enable
using System;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Events;
using _VuTH.Core.Persistant.SaveSystem.Migrate;
using _VuTH.Core.Persistant.SaveSystem.Profile;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;

#if VCONTAINER
using VContainer;
#endif

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// SaveServiceManager - BootstrapManager that wraps SaveService core logic.
    /// Implements ISaveManager and provides lifecycle management.
    /// Loads adapters from SaveServiceAdapterProfile (Resources) if available.
    /// Falls back to defaults if profile not found or adapters are null.
    /// </summary>
    public class SaveServiceManager : VBootstrapManager<SaveServiceManager, ISaveManager>, ISaveManager
    {
        // Core components
        private ISaveBackend? _backend;
        private ISerializer? _serializer;
        private IEncryptor? _encryptor;
        private ISaveEventPublisher? _eventPublisher;
        private SaveMigrationChain? _migrationChain;
        private int _currentSchemaVersion = 1;

        // Profile reference
        private SaveServiceAdapterProfile? _profile;

        // Internal save service for delegation
        private SaveService? _saveService;

        #region ISaveService Implementation

        public async UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default)
        {
            if (_saveService == null)
            {
                throw new InvalidOperationException("SaveServiceManager not initialized. Call InitializeBootstrap first.");
            }

            await _saveService.SaveAsync(key, data, cancellationToken);
        }

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            if (_saveService == null)
            {
                throw new InvalidOperationException("SaveServiceManager not initialized. Call InitializeBootstrap first.");
            }

            return await _saveService.LoadAsync(key, defaultValue, cancellationToken);
        }

        public async UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_saveService == null)
            {
                throw new InvalidOperationException("SaveServiceManager not initialized. Call InitializeBootstrap first.");
            }

            return await _saveService.ExistsAsync(key, cancellationToken);
        }

        public async UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_saveService == null)
            {
                throw new InvalidOperationException("SaveServiceManager not initialized. Call InitializeBootstrap first.");
            }

            await _saveService.DeleteAsync(key, cancellationToken);
        }

        #endregion

        #region Bootstrap Lifecycle

#if VCONTAINER
        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            builder.RegisterInstance<ISaveManager>(this);
        }
#endif

        protected override void InitializeBootstrap()
        {
            this.Log("Initializing Save Service...");

            // Load profile from Resources
            _profile = Resources.Load<SaveServiceAdapterProfile>("SaveServiceAdapterProfile");

            InitializeSaveService(null);
        }

        private void InitializeSaveService(IPublisher<SaveEvent>? eventPublisher)
        {
            if (_profile != null)
            {
                this.Log("SaveServiceAdapterProfile loaded successfully.");
                InitializeFromProfile(eventPublisher);
            }
            else
            {
                this.Log("No SaveServiceAdapterProfile found. Using default configuration.");
                InitializeWithDefaults(eventPublisher);
            }

            // Create the internal save service
            _saveService = new SaveService(
                _backend!,
                _serializer!,
                _encryptor!,
                _currentSchemaVersion,
                _eventPublisher
            );

            // Add migrators from profile if available
            if (_profile && _migrationChain != null)
            {
                foreach (var migrator in _profile.Migrators)
                {
                    _migrationChain.AddMigrator(migrator);
                }
            }

            this.Log("Save Service initialized successfully.");
        }

        private void InitializeFromProfile(IPublisher<SaveEvent>? eventPublisher)
        {
            // Use profile adapters if available, otherwise fall back to defaults
            _encryptor = _profile!.Encryptor;
            _serializer = _profile.Serializer;
            _eventPublisher = eventPublisher != null
                ? new MessagePipeSaveEventPublisher(eventPublisher)
                : new NullSaveEventPublisher();

            if (_profile.Backend != null)
            {
                _backend = _profile.Backend;
            }
            else
            {
                // Fallback backend based on environment
#if UNITY_EDITOR
                _backend = new JsonFileSaveBackend();
#else
                _backend = new PlayerPrefsSaveBackend();
#endif
            }

            // Initialize migration chain with serializer from profile
            _migrationChain = new SaveMigrationChain(_serializer!);
        }

        private void InitializeWithDefaults(IPublisher<SaveEvent>? eventPublisher)
        {
#if UNITY_EDITOR
            _backend = new JsonFileSaveBackend();
#else
            _backend = new PlayerPrefsSaveBackend();
#endif
            _serializer = new NewtonsoftJsonSerializer();
            _encryptor = new XorEncryptor();
            _eventPublisher = eventPublisher != null
                ? new MessagePipeSaveEventPublisher(eventPublisher)
                : new NullSaveEventPublisher();
            _migrationChain = new SaveMigrationChain(_serializer!);
        }

        protected override void DeinitializeBootstrap()
        {
            this.Log("Deinitializing Save Service...");

            _saveService = null;
            _backend = null;
            _serializer = null;
            _encryptor = null;
            _eventPublisher = null;
            _migrationChain = null;
            _profile = null;

            this.Log("Save Service deinitialized.");
        }

        #endregion

        #region Configuration Methods

        /// <summary>
        /// Gets the loaded profile (null if not loaded).
        /// </summary>
        public SaveServiceAdapterProfile? GetProfile() => _profile;

        /// <summary>
        /// Checks if a profile is loaded.
        /// </summary>
        public bool HasProfile => _profile != null;

        /// <summary>
        /// Configures the backend. Call before InitializeBootstrap.
        /// </summary>
        public SaveServiceManager WithBackend(ISaveBackend backend)
        {
            _backend = backend;
            return this;
        }

        /// <summary>
        /// Configures the serializer. Call before InitializeBootstrap.
        /// </summary>
        public SaveServiceManager WithSerializer(ISerializer serializer)
        {
            _serializer = serializer;
            return this;
        }

        /// <summary>
        /// Configures the encryptor. Call before InitializeBootstrap.
        /// </summary>
        public SaveServiceManager WithEncryptor(IEncryptor encryptor)
        {
            _encryptor = encryptor;
            return this;
        }

        /// <summary>
        /// Configures the event publisher. Call before InitializeBootstrap.
        /// </summary>
        public SaveServiceManager WithEventPublisher(ISaveEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
            return this;
        }

        /// <summary>
        /// Configures the schema version. Call before InitializeBootstrap.
        /// </summary>
        public SaveServiceManager WithSchemaVersion(int version)
        {
            _currentSchemaVersion = version;
            return this;
        }

        /// <summary>
        /// Gets the current schema version.
        /// </summary>
        public int GetSchemaVersion() => _currentSchemaVersion;

        /// <summary>
        /// Checks if the service is initialized.
        /// </summary>
        public bool IsInitialized => _saveService != null;

        #endregion
    }
}
