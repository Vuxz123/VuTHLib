#nullable enable
using System;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using _VuTH.Core.Persistent.SaveSystem.Events;
using _VuTH.Core.Persistent.SaveSystem.Migrate;
using _VuTH.Core.Persistent.SaveSystem.Profile;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

#if VCONTAINER
using VContainer;
#endif

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// SaveServiceManager - BootstrapManager that wraps SaveService core logic.
    /// Implements ISaveManager and provides lifecycle management.
    /// Loads adapters from SaveServiceAdapterProfile (Resources) if available.
    /// Falls back to defaults if profile not found or adapters are null.
    /// </summary>
    public class SaveServiceManager : VBootstrapManager<SaveServiceManager, ISaveManager>, ISaveManager, ISyncSaveService
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

        /// <inheritdoc />
        public bool TrySaveImmediate<T>(string key, T data)
        {
            return _saveService != null && _saveService.TrySaveImmediate(key, data);
        }

        #endregion

        #region Bootstrap Lifecycle

        // Injected SaveEvent publisher (null without DI: events go to a null publisher).
        private IPublisher<SaveEvent>? _injectedEventPublisher;

#if VCONTAINER
        /// <summary>Stores the event publisher; the service is built in <see cref="InitializeBootstrap"/>.</summary>
        [Inject]
        public void Construct(IPublisher<SaveEvent> eventPublisher)
        {
            _injectedEventPublisher = eventPublisher;

            // Injected after initialization (manager placed in a scene rather than created by the bootstrap):
            // rebuild the service so it publishes SaveEvents.
            if (IsManagerInitialized) InitializeSaveService(eventPublisher);
        }
#endif

        protected override void InitializeBootstrap()
        {
            this.Log("Initializing Save Service...");

            // Load profile from Resources
            _profile = Resources.Load<SaveServiceAdapterProfile>("SaveServiceAdapterProfile");
            this.Log(_profile != null
                ? "InitializeBootstrap: SaveServiceAdapterProfile found."
                : "InitializeBootstrap: SaveServiceAdapterProfile not found.");

            InitializeSaveService(_injectedEventPublisher);
        }

        /// <summary>
        /// Rebuilds the save service, e.g. after a test replaced the backend (the service is first built in Awake).
        /// </summary>
        public void InitializeForTesting()
        {
            InitializeSaveService(_injectedEventPublisher);
        }

        private void InitializeSaveService(IPublisher<SaveEvent>? eventPublisher)
        {
            this.Log($"InitializeSaveService: called. eventPublisherNull={eventPublisher == null}");

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

            // Add migrators from profile if available
            if (_profile && _migrationChain != null)
            {
                foreach (var migrator in _profile.Migrators)
                {
                    _migrationChain.AddMigrator(migrator);
                }
            }

            // Create the internal save service
            _saveService = new SaveService(
                _backend!,
                _serializer!,
                _encryptor!,
                _currentSchemaVersion,
                _migrationChain,
                _eventPublisher
            );

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

            // Only use profile backend if _backend wasn't manually set (e.g., by tests)
            if (_backend == null)
            {
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
            }
            else
            {
                this.Log("InitializeFromProfile: Using manually set backend (e.g., from test).");
            }

            // Initialize migration chain with serializer from profile
            _migrationChain = new SaveMigrationChain(_serializer!);
        }

        private void InitializeWithDefaults(IPublisher<SaveEvent>? eventPublisher)
        {
            // Only initialize backend if not manually set (e.g., by tests)
            if (_backend == null)
            {
#if UNITY_EDITOR
                _backend = new JsonFileSaveBackend();
#else
                _backend = new PlayerPrefsSaveBackend();
#endif
            }
            else
            {
                this.Log("InitializeWithDefaults: Using manually set backend (e.g., from test).");
            }

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
        public override bool IsInitialized => _saveService != null;

        #endregion
    }
}
