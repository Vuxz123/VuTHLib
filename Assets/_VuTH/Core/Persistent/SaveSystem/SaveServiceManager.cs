#nullable enable
using System;
using System.Collections.Generic;
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

#if VCONTAINER
using VContainer;
#endif

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// Bootstrap manager exposing the save pipeline (<see cref="ISaveManager"/>).
    /// <para>
    /// Configuration, highest priority first: values set in code (<c>With*</c>), then the
    /// <see cref="SaveServiceAdapterProfile"/> in Resources, then the defaults. <c>With*</c> calls can be made at any
    /// time: an initialized manager rebuilds its service right away.
    /// </para>
    /// </summary>
    public class SaveServiceManager : VBootstrapManager<SaveServiceManager, ISaveManager>, ISaveManager
    {
        // Values set in code; they take precedence over the profile.
        private ISaveBackend? _backendOverride;
        private ISerializer? _serializerOverride;
        private IEncryptor? _encryptorOverride;
        private ISaveEventPublisher? _eventPublisherOverride;
        private int? _schemaVersionOverride;
        private readonly List<ISaveMigrator> _additionalMigrators = new();

        // Injected SaveEvent publisher (null without DI: events go to a null publisher).
        private IPublisher<SaveEvent>? _injectedEventPublisher;

        private SaveServiceAdapterProfile? _profile;
        private SaveService? _saveService;

        #region ISaveService Implementation

        private SaveService Service => _saveService ?? throw new InvalidOperationException(
            "SaveServiceManager is not initialized yet. Give it a lower bootstrap order than the managers using it.");

        public UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default) =>
            Service.SaveAsync(key, data, cancellationToken);

        public UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Service.TryLoadAsync<T>(key, cancellationToken);

        public UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default) =>
            Service.LoadAsync(key, defaultValue, cancellationToken);

        public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Service.ExistsAsync(key, cancellationToken);

        public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            Service.DeleteAsync(key, cancellationToken);

        /// <inheritdoc />
        public bool SupportsSyncSave => _saveService != null && _saveService.SupportsSyncSave;

        /// <inheritdoc />
        public bool TrySaveImmediate<T>(string key, T data)
        {
            return _saveService != null && _saveService.TrySaveImmediate(key, data);
        }

        public UniTask SaveAsync<T>(string key, T data, SaveSchema schema, CancellationToken cancellationToken = default) =>
            Service.SaveAsync(key, data, schema, cancellationToken);

        public UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, SaveSchema schema, CancellationToken cancellationToken = default) =>
            Service.TryLoadAsync<T>(key, schema, cancellationToken);

        /// <inheritdoc />
        public bool TrySaveImmediate<T>(string key, T data, SaveSchema schema)
        {
            return _saveService != null && _saveService.TrySaveImmediate(key, data, schema);
        }

        #endregion

        #region Bootstrap Lifecycle

#if VCONTAINER
        /// <summary>Stores the event publisher; the service is built in <see cref="InitializeBootstrap"/>.</summary>
        [Inject]
        public void Construct(IPublisher<SaveEvent> eventPublisher)
        {
            _injectedEventPublisher = eventPublisher;

            // Injected after initialization (manager placed in a scene rather than created by the bootstrap):
            // rebuild the service so it publishes SaveEvents.
            if (IsManagerInitialized) BuildService();
        }
#endif

        protected override void InitializeBootstrap()
        {
            _profile = Resources.Load<SaveServiceAdapterProfile>(SaveServiceAdapterProfile.ResourcePath);
            if (_profile == null)
            {
                this.Log($"No SaveServiceAdapterProfile in Resources/{SaveServiceAdapterProfile.ResourcePath}: using defaults.");
            }

            BuildService();
        }

        protected override void DeinitializeBootstrap()
        {
            _saveService = null;
            _profile = null;
        }

        /// <summary>
        /// Rebuilds the service from the current configuration. <c>With*</c> calls already do this; call it after
        /// changing the profile at runtime.
        /// </summary>
        public void Rebuild()
        {
            if (IsManagerInitialized) BuildService();
        }

        private void BuildService()
        {
            // Explicit Unity null check: a destroyed profile must count as missing.
            var profile = _profile != null ? _profile : null;
            var hasProfile = profile != null;

            var backend = _backendOverride
                          ?? (hasProfile ? profile!.Backend : null)
                          ?? SaveServiceAdapterProfile.CreateDefaultBackend();
            var serializer = _serializerOverride
                             ?? (hasProfile ? profile!.Serializer : null)
                             ?? SaveServiceAdapterProfile.CreateDefaultSerializer();
            var encryptor = _encryptorOverride
                            ?? (hasProfile ? profile!.Encryptor : null)
                            ?? SaveServiceAdapterProfile.CreateDefaultEncryptor();
            var schemaVersion = _schemaVersionOverride ?? (hasProfile ? profile!.SchemaVersion : SaveService.DefaultSchemaVersion);
            var eventPublisher = _eventPublisherOverride
                                 ?? (_injectedEventPublisher != null
                                     ? new MessagePipeSaveEventPublisher(_injectedEventPublisher)
                                     : new NullSaveEventPublisher());

            var migrationChain = new SaveMigrationChain();
            if (hasProfile) AddMigrators(migrationChain, profile!.Migrators);
            AddMigrators(migrationChain, _additionalMigrators);

            _saveService = new SaveService(backend, serializer, encryptor, schemaVersion, migrationChain, eventPublisher);
            this.Log($"Save service ready: {backend.GetType().Name}, {serializer.GetType().Name}, " +
                     $"{encryptor.GetType().Name}, schema v{schemaVersion}, {migrationChain.Count} migrator(s).");
        }

        private void AddMigrators(SaveMigrationChain chain, IEnumerable<ISaveMigrator> migrators)
        {
            foreach (var migrator in migrators)
            {
                if (migrator == null) continue;

                try
                {
                    chain.AddMigrator(migrator);
                }
                catch (ArgumentException e)
                {
                    // Saves needing this step will report MigrationFailed (and stay untouched) instead of loading.
                    this.LogError($"Ignored save migrator: {e.Message}");
                }
            }
        }

        #endregion

        #region Configuration

        /// <summary>The profile loaded from Resources (null if none).</summary>
        public SaveServiceAdapterProfile? GetProfile() => _profile;

        public bool HasProfile => _profile != null;

        /// <summary>Schema version written by the current service.</summary>
        public int GetSchemaVersion() => _saveService?.SchemaVersion
                                         ?? _schemaVersionOverride
                                         ?? (_profile != null ? _profile.SchemaVersion : SaveService.DefaultSchemaVersion);

        /// <summary>Uses <paramref name="backend"/> instead of the profile's (null restores the profile's).</summary>
        public SaveServiceManager WithBackend(ISaveBackend? backend)
        {
            _backendOverride = backend;
            Rebuild();
            return this;
        }

        /// <summary>Uses <paramref name="serializer"/> instead of the profile's (null restores the profile's).</summary>
        public SaveServiceManager WithSerializer(ISerializer? serializer)
        {
            _serializerOverride = serializer;
            Rebuild();
            return this;
        }

        /// <summary>Uses <paramref name="encryptor"/> instead of the profile's (null restores the profile's).</summary>
        public SaveServiceManager WithEncryptor(IEncryptor? encryptor)
        {
            _encryptorOverride = encryptor;
            Rebuild();
            return this;
        }

        /// <summary>Uses <paramref name="eventPublisher"/> instead of MessagePipe (null restores MessagePipe).</summary>
        public SaveServiceManager WithEventPublisher(ISaveEventPublisher? eventPublisher)
        {
            _eventPublisherOverride = eventPublisher;
            Rebuild();
            return this;
        }

        /// <summary>Uses <paramref name="version"/> instead of the profile's schema version (null restores it).</summary>
        public SaveServiceManager WithSchemaVersion(int? version)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version), "Schema version cannot be negative.");
            _schemaVersionOverride = version;
            Rebuild();
            return this;
        }

        /// <summary>Adds a migrator to the profile's.</summary>
        public SaveServiceManager WithMigrator(ISaveMigrator migrator)
        {
            _additionalMigrators.Add(migrator ?? throw new ArgumentNullException(nameof(migrator)));
            Rebuild();
            return this;
        }

        /// <summary>Whether the service is built.</summary>
        public override bool IsInitialized => _saveService != null;

        #endregion
    }
}
