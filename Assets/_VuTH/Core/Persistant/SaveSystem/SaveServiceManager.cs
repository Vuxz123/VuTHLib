#nullable enable
using System;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Events;
using _VuTH.Core.Persistant.SaveSystem.Migrate;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using Cysharp.Threading.Tasks;

#if VCONTAINER
using VContainer;
#endif

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// SaveServiceManager - BootstrapManager that wraps SaveService core logic.
    /// Implements ISaveService and provides lifecycle management.
    /// Follows the project's VBootstrapManager pattern.
    /// </summary>
    public class SaveServiceManager : VBootstrapManager<SaveServiceManager, ISaveManager>, ISaveManager
    {
        // Core components (injected via VContainer or created in InitializeBootstrap)
        private ISaveBackend? _backend;
        private ISerializer? _serializer;
        private IEncryptor? _encryptor;
        private ISaveEventPublisher? _eventPublisher;
        private SaveMigrationChain? _migrationChain;
        private int _currentSchemaVersion = 1;

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
            this.Log("[SaveServiceManager] Initializing Save Service...");

            // Create default components if not injected
            _backend ??= new PlayerPrefsSaveBackend();
            _serializer ??= new JsonSerializer();
            _encryptor ??= new XorEncryptor();
            _eventPublisher ??= new NullSaveEventPublisher();
            _migrationChain ??= new SaveMigrationChain(_serializer);

            // Create the internal save service
            _saveService = new SaveService(
                _backend,
                _serializer,
                _encryptor,
                _currentSchemaVersion,
                _eventPublisher
            );

            this.Log("[SaveServiceManager] Save Service initialized successfully.");
        }

        protected override void DeinitializeBootstrap()
        {
            this.Log("[SaveServiceManager] Deinitializing Save Service...");

            _saveService = null;
            _backend = null;
            _serializer = null;
            _encryptor = null;
            _eventPublisher = null;
            _migrationChain = null;

            this.Log("[SaveServiceManager] Save Service deinitialized.");
        }

        #endregion

        #region Configuration Methods

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
