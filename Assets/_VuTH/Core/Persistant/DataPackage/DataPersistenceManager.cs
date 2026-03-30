#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Persistant.SaveSystem;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ZLinq;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Manager that orchestrates persistence packages and save system.
    /// Handles all save pipeline setup — packages only hold data.
    /// Supports both VContainer DI and non-VContainer modes via VCONTAINER macro.
    /// </summary>
    /// <remarks>
    /// Package initialization is started asynchronously and is not a blocking bootstrap barrier for other managers.
    /// Resolving a package instance does not guarantee that its persisted data has finished loading.
    /// <see cref="IsInitialized"/> only becomes true after the initially configured packages have completed loading.
    /// Packages registered later via <see cref="RegisterPackage"/> are initialized asynchronously as well and are not guaranteed
    /// to be ready immediately after registration returns.
    /// Consumers that require loaded persistence data must explicitly wait for a readiness signal instead of assuming that
    /// registration, resolution, or manager construction implies load completion.
    /// </remarks>
    public class DataPersistenceManager : VBootstrapManager<DataPersistenceManager, IDataPersistenceManager>, IDataPersistenceManager
    {
        private ISaveService? _saveService;
        private SaveLifecycleHook? _lifecycleHook;

        private readonly List<IPersistencePackage> _configuredPackages = new();
        private readonly List<IPersistencePackage> _packages = new();
        private readonly Dictionary<IPersistencePackage, IDisposable> _saveSubscriptions = new();
        private readonly HashSet<IPersistencePackage> _initializedPackages = new();
        private readonly HashSet<IPersistencePackage> _initializingPackages = new();
        private CancellationTokenSource? _initializationCts;
        private bool _configuredPackagesLoaded;
        private bool _initializationStarted;
        private bool _initialized;

        #region VContainer DI

#if VCONTAINER
        [Inject]
        public void Construct(ISaveManager saveManager, SaveLifecycleHook lifecycleHook, IReadOnlyList<IPersistencePackage> packages)
        {
            _saveService = saveManager;
            _lifecycleHook = lifecycleHook;
            
            if (_saveService == null)
            {
                Debug.LogError("[DataPersistenceManager] ISaveManager does not implement ISaveService!");
            }

            BeginInitializePackages(packages);
        }
        
        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            foreach (var package in GetConfiguredPackages())
            {
                builder.RegisterInstance(package).As<IPersistencePackage>();
            }

            builder.RegisterComponent(this).AsImplementedInterfaces();
            builder.Register<SaveLifecycleHook>(Lifetime.Singleton);
        }
#endif

        #endregion

        #region Bootstrap Lifecycle

        protected override void InitializeBootstrap()
        {
            this.Log("Initializing Data Persistence Manager...");

#if VCONTAINER
            this.Log("DataPersistenceManager: VCONTAINER defined - waiting for DI Construct()");
#else
            this.Log("DataPersistenceManager: VCONTAINER not defined - using fallback initialization");
            InitializeWithoutVContainer();
#endif
        }

        /// <summary>
        /// Fallback initialization for non-VContainer builds (e.g., tests).
        /// </summary>
        public void InitializeWithoutVContainer()
        {
            if (SaveServiceManager.HasInstance)
            {
                _saveService = SaveServiceManager.Instance;
            }
            
            if (_saveService == null)
            {
                this.LogError("Cannot find ISaveService! Make sure SaveServiceManager is initialized.");
                return;
            }

            BeginInitializePackages(GetConfiguredPackages());
        }

        private void BeginInitializePackages(IReadOnlyList<IPersistencePackage> initialPackages)
        {
            foreach (var package in initialPackages)
            {
                if (_packages.Contains(package)) continue;
                _packages.Add(package);
            }

            if (_initializationStarted) return;
            _initializationStarted = true;
            _initializationCts = new CancellationTokenSource();
            InitializePackagesAsync(_initializationCts.Token).Forget();
        }

        private async UniTaskVoid InitializePackagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!await WaitForSaveServiceReadyAsync(cancellationToken))
                {
                    return;
                }

                while (TryGetNextPendingPackage(out var package))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await InitializePackageAsync(package, cancellationToken);
                }

                _initialized = true;
                this.Log($"Initialized {_packages.Count} persistence packages");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                this.LogError($"Exception during package initialization: {e}");
                Debug.LogException(e);
            }
        }

        private bool TryGetNextPendingPackage(out IPersistencePackage package)
        {
            for (var i = 0; i < _packages.Count; i++)
            {
                var candidate = _packages[i];
                if (_initializedPackages.Contains(candidate) || _initializingPackages.Contains(candidate))
                {
                    continue;
                }

                package = candidate;
                return true;
            }

            package = null!;
            return false;
        }

        private async UniTask<bool> WaitForSaveServiceReadyAsync(CancellationToken cancellationToken)
        {
            if (_saveService == null)
            {
                this.LogError("Cannot initialize packages because ISaveService is missing.");
                return false;
            }

            if (_saveService is not SaveServiceManager saveServiceManager)
            {
                return true;
            }

            const int maxFramesToWait = 300;
            var waitedFrames = 0;

            while (!saveServiceManager.IsInitialized && waitedFrames < maxFramesToWait)
            {
                cancellationToken.ThrowIfCancellationRequested();
                waitedFrames++;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (saveServiceManager.IsInitialized)
            {
                return true;
            }

            this.LogError("SaveServiceManager was injected but never finished initialization before persistence packages were loaded.");
            return false;
        }

        private async UniTask InitializePackageAsync(IPersistencePackage package, CancellationToken cancellationToken)
        {
            if (_initializedPackages.Contains(package) ||
                _initializingPackages.Contains(package) ||
                !_packages.Contains(package))
            {
                return;
            }

            _initializingPackages.Add(package);

            try
            {
                SetupSavePipeline(package);

                if (_saveService == null)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                package.SetSaveService(_saveService);
                await package.LoadAsync();

                if (!_packages.Contains(package))
                {
                    this.Log($"Skipped completion for unregistered package: {package.StorageKey}");
                    return;
                }

                _initializedPackages.Add(package);
                this.Log($"Initialized package: {package.StorageKey}");
            }
            finally
            {
                _initializingPackages.Remove(package);
            }
        }

        private IReadOnlyList<IPersistencePackage> GetConfiguredPackages()
        {
            if (_configuredPackagesLoaded) return _configuredPackages;
            _configuredPackagesLoaded = true;

            if (!DataPackageProfileUtilities.TryGetProfile(out var profile) || profile == null)
            {
                this.Log("No DataPackageProfile found. Skipping configured package registration.");
                return _configuredPackages;
            }

            foreach (var typeName in profile.PackageTypeNames)
            {
                if (!PersistencePackageFactory.TryCreate(typeName, out var package) || package == null)
                {
                    continue;
                }

                if (_configuredPackages.AsValueEnumerable().Any(existing => existing.GetType() == package.GetType()))
                {
                    continue;
                }

                _configuredPackages.Add(package);
            }

            this.Log($"Loaded {_configuredPackages.Count} configured persistence packages from profile.");
            return _configuredPackages;
        }

        /// <summary>
        /// Setup save pipeline based on package strategy.
        /// Manager subscribes to package's DirtyObservable and triggers saves accordingly.
        /// </summary>
        private void SetupSavePipeline(IPersistencePackage package)
        {
            // Unsubscribe existing if any
            if (_saveSubscriptions.TryGetValue(package, out var existing))
            {
                existing.Dispose();
                _saveSubscriptions.Remove(package);
            }

            var subscriptionGroup = new SubscriptionGroup();

            switch (package.Strategy)
            {
                case SaveStrategy.Immediate:
                    subscriptionGroup.Add(package.DirtyObservable
                        .Where(dirty => dirty)
                        .Subscribe(Save));
                    break;

                case SaveStrategy.Debounced:
                    subscriptionGroup.Add(package.DirtyObservable
                        .Where(dirty => dirty)
                        .ThrottleLast(TimeSpan.FromSeconds(package.DebounceSeconds))
                        .Subscribe(Save));
                    break;

                case SaveStrategy.ManualOnly:
                    break;
                case SaveStrategy.OnAppClose:
                    if (_lifecycleHook != null)
                    {
                        subscriptionGroup.Add(_lifecycleHook.RegisterPackage(package));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!subscriptionGroup.IsEmpty)
            {
                _saveSubscriptions[package] = subscriptionGroup;
            }

            return;

            void Save(bool _)
            {
                package.SaveNow();
            }
        }

        protected override void DeinitializeBootstrap()
        {
            var dirtyPackages = _packages
                .AsValueEnumerable()
                .Where(package => package.IsDirty)
                .ToList();

            FlushDirtyPackagesAsync(dirtyPackages).Forget();
            
            // Dispose all subscriptions
            foreach (var kvp in _saveSubscriptions)
            {
                kvp.Value.Dispose();
            }
            _saveSubscriptions.Clear();

            _initializationCts?.Cancel();
            _initializationCts?.Dispose();
            _initializationCts = null;

            _packages.Clear();
            _initializedPackages.Clear();
            _initializingPackages.Clear();
            _packageTypeCache.Clear();
            _initializationStarted = false;
            _initialized = false;
            this.Log("DataPersistenceManager deinitialized.");
        }

        private async UniTask FlushDirtyPackagesAsync(IReadOnlyList<IPersistencePackage> packages)
        {
            foreach (var package in packages)
            {
                await package.SaveNowAsync();
            }
        }

        #endregion

        #region Package Management

        /// <summary>
        /// Register a persistence package to be managed.
        /// If strategy is OnAppClose or ManualOnly, auto-registers to SaveLifecycleHook.
        /// </summary>
        /// <remarks>
        /// Packages registered during bootstrap are queued and initialized by the active bootstrap pass.
        /// Packages registered after bootstrap still initialize asynchronously.
        /// Returning from this method does not guarantee that the package has finished loading its persisted data.
        /// </remarks>
        public void RegisterPackage(IPersistencePackage package)
        {
            if (_packages.Contains(package))
            {
                this.Log($"Skipped duplicate package registration: {package.StorageKey}");
                return;
            }

            _packages.Add(package);
            this.Log($"Registered package: {package.StorageKey}");

            if (!_initializationStarted)
            {
                this.Log($"Package {package.StorageKey} queued before persistence initialization starts.");
                return;
            }

            if (!_initialized)
            {
                this.Log($"Package {package.StorageKey} queued for bootstrap-time initialization.");
                return;
            }

            InitializeLateRegisteredPackageAsync(package).Forget();
        }

        /// <summary>
        /// Unregister a persistence package.
        /// </summary>
        public void UnregisterPackage(IPersistencePackage package)
        {
            if (!_packages.Remove(package)) return;

            _initializedPackages.Remove(package);
            _initializingPackages.Remove(package);
            _packageTypeCache.Remove(package.GetType());

            if (_saveSubscriptions.TryGetValue(package, out var sub))
            {
                sub.Dispose();
                _saveSubscriptions.Remove(package);
            }
            
            SavePackageBeforeUnregisterAsync(package).Forget();
            this.Log($"Unregistered package: {package.StorageKey}");
        }

        private async UniTask SavePackageBeforeUnregisterAsync(IPersistencePackage package)
        {
            if (package.IsDirty)
            {
                await package.SaveNowAsync();
            }
        }

        /// <summary>
        /// Force save all dirty packages.
        /// </summary>
        public void SaveAll()
        {
            SaveAllAsync().Forget();
        }

        public async UniTask SaveAllAsync()
        {
            foreach (var package in _packages.AsValueEnumerable().Where(package => package.IsDirty))
            {
                await package.SaveNowAsync();
            }

            this.Log("Saved all dirty packages");
        }

        /// <summary>
        /// Load all packages from storage.
        /// </summary>
        /// <remarks>
        /// This method triggers <see cref="LoadAllAsync"/> via <c>Forget()</c> and returns immediately.
        /// Use <see cref="LoadAllAsync"/> when the caller must await completion.
        /// </remarks>
        public void LoadAll()
        {
            LoadAllAsync().Forget();
        }

        public async UniTask LoadAllAsync()
        {
            foreach (var package in _packages)
            {
                await package.LoadAsync();
            }

            this.Log("Loaded all packages");
        }

        /// <summary>
        /// Gets whether the initially configured package set has completed asynchronous initialization.
        /// </summary>
        /// <remarks>
        /// This flag does not mean that every package ever registered with the manager is fully loaded.
        /// Packages added later through <see cref="RegisterPackage"/> may still be loading after this becomes true.
        /// </remarks>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Get the number of registered packages.
        /// </summary>
        public int PackageCount => _packages.Count;

        #endregion

        #region Package Retrieval

        private readonly Dictionary<Type, IPersistencePackage> _packageTypeCache = new();
        
        public T? GetPackage<T>() where T : class, IPersistencePackage
        {
            var type = typeof(T);
            if (_packageTypeCache.TryGetValue(type, out var cachedPackage))
            {
                if (!_packages.Contains(cachedPackage))
                {
                    _packageTypeCache.Remove(type);
                }
                else
                {
                    return cachedPackage as T;
                }
            }

            var package = _packages.AsValueEnumerable().FirstOrDefault(p => p.GetType() == type);
            if (package != null)
            {
                _packageTypeCache[type] = package;
                return package as T;
            }

            this.LogWarning($"Requested package of type {type.Name} not found.");
            return null;
        }
        
        public bool TryGetPackage<T>(out T? package) where T : class, IPersistencePackage
        {
            package = GetPackage<T>();
            return package != null;
        }

        private async UniTaskVoid InitializeLateRegisteredPackageAsync(IPersistencePackage package)
        {
            try
            {
                if (_initializationCts == null)
                {
                    this.LogWarning($"Skipped late registration init because manager is not active: {package.StorageKey}");
                    return;
                }

                await InitializePackageAsync(package, _initializationCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        #endregion

        private sealed class SubscriptionGroup : IDisposable
        {
            private readonly List<IDisposable> _subscriptions = new();

            public bool IsEmpty => _subscriptions.Count == 0;

            public void Add(IDisposable subscription)
            {
                _subscriptions.Add(subscription);
            }

            public void Dispose()
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Dispose();
                }

                _subscriptions.Clear();
            }
        }
    }
}
