#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Persistent.SaveSystem;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ZLinq;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Manager that orchestrates persistence packages and save system.
    /// Handles all save pipeline setup — packages only hold data.
    /// Supports both VContainer DI and non-VContainer modes via VCONTAINER macro.
    /// </summary>
    /// <remarks>
    /// Package initialization is started asynchronously and is not a blocking bootstrap barrier for other managers.
    /// Resolving a package instance does not guarantee that its persisted data has finished loading.
    /// <see cref="IsInitialized"/> only becomes true after the initially configured packages have completed loading; await
    /// <see cref="WaitUntilReadyAsync"/> for that. Packages registered later via <see cref="RegisterPackage"/> load
    /// asynchronously as well: await their <see cref="IPersistencePackage.WaitUntilLoadedAsync"/>.
    /// </remarks>
    public class DataPersistenceManager : VBootstrapManager<DataPersistenceManager, IDataPersistenceManager>, IDataPersistenceManager
    {
        private ISaveService? _saveService;
        private SaveLifecycleHook? _lifecycleHook;
        private IReadOnlyList<IPersistencePackage>? _injectedPackages;

        private readonly List<IPersistencePackage> _configuredPackages = new();
        private readonly List<IPersistencePackage> _packages = new();
        private readonly Dictionary<IPersistencePackage, IDisposable> _saveSubscriptions = new();
        private readonly HashSet<IPersistencePackage> _initializedPackages = new();
        private readonly HashSet<IPersistencePackage> _initializingPackages = new();
        private CancellationTokenSource? _initializationCts;
        private UniTaskCompletionSource _readySource = new();
        private bool _configuredPackagesLoaded;
        private bool _initializationStarted;
        private bool _initialized;

        #region VContainer DI

#if VCONTAINER
        /// <summary>Stores dependencies only; packages start loading in <see cref="InitializeBootstrap"/>.</summary>
        [Inject]
        public void Construct(ISaveManager saveManager, SaveLifecycleHook lifecycleHook, IReadOnlyList<IPersistencePackage> packages)
        {
            _saveService = saveManager;
            _lifecycleHook = lifecycleHook;
            _injectedPackages = packages;

            // Injected after initialization (manager placed in a scene rather than created by the bootstrap).
            if (IsManagerInitialized) BeginInitializePackages(packages);
        }


        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            foreach (var package in GetConfiguredPackages())
            {
                // Also as its concrete type, so consumers can inject e.g. PlayerProfilePackage directly.
                builder.RegisterInstance(package).AsSelf();
            }

            RegisterSelf(builder);
            RegisterLifecycleHook(builder);
        }

        /// <summary>
        /// Registers <see cref="SaveLifecycleHook"/> as an ENTRY POINT so VContainer calls its
        /// <c>IInitializable.Initialize()</c> (subscribing to focus / pause / quit) and <c>Dispose()</c>.
        /// A plain <c>Register&lt;SaveLifecycleHook&gt;()</c> is only resolvable as its concrete type: Initialize was
        /// never called, so the pause / quit flushes never ran.
        /// </summary>
        internal static void RegisterLifecycleHook(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<SaveLifecycleHook>().AsSelf();
        }
#endif

        #endregion

        #region Bootstrap Lifecycle

        protected override void InitializeBootstrap()
        {
            this.Log("Initializing Data Persistence Manager...");

#if VCONTAINER
            if (_saveService != null)
            {
                BeginInitializePackages(_injectedPackages ?? GetConfiguredPackages());
                return;
            }

            // Not injected (not created through the root scope): fall back to the singletons.
#endif
            InitializeWithoutVContainer();
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
                const string error = "Cannot find ISaveService! Make sure SaveServiceManager is initialized.";
                this.LogError(error);
                _readySource.TrySetException(new InvalidOperationException(error));
                return;
            }

            BeginInitializePackages(GetConfiguredPackages());
        }

        private void BeginInitializePackages(IReadOnlyList<IPersistencePackage> initialPackages)
        {
            foreach (var package in initialPackages)
            {
                TryAddPackage(package);
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
                if (!IsSaveServiceReady(out var error))
                {
                    this.LogError(error);
                    _readySource.TrySetException(new InvalidOperationException(error));
                    return;
                }

                while (TryGetNextPendingPackage(out var package))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await InitializePackageAsync(package, cancellationToken);
                }

                _initialized = true;
                _readySource.TrySetResult();
                this.Log($"Initialized {_packages.Count} persistence packages");
            }
            catch (OperationCanceledException)
            {
                _readySource.TrySetCanceled();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                _readySource.TrySetException(e);
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

        private bool IsSaveServiceReady(out string error)
        {
            if (_saveService == null)
            {
                error = "Cannot initialize packages because ISaveService is missing.";
                return false;
            }

            // The save manager initializes first (lower bootstrap order), so it is ready here: no polling.
            if (_saveService is ICommonManager { IsInitialized: false })
            {
                error = "The save manager is not initialized. Give it a lower bootstrap order than the " +
                        "DataPersistenceManager so it initializes first.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>Adds a package unless it is already managed or its storage key is taken.</summary>
        private bool TryAddPackage(IPersistencePackage package)
        {
            if (_packages.Contains(package)) return false;

            foreach (var existing in _packages)
            {
                if (existing.StorageKey != package.StorageKey) continue;

                this.LogError($"Package {package.GetType().Name} uses the storage key '{package.StorageKey}' of " +
                              $"{existing.GetType().Name}; it is not registered, as they would overwrite each other's save.");
                return false;
            }

            _packages.Add(package);
            return true;
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
                    // Changes still waiting for the debounce window would be lost if the app is paused/killed.
                    if (_lifecycleHook != null)
                    {
                        subscriptionGroup.Add(_lifecycleHook.RegisterPackage(package));
                    }
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
            // Deinit usually runs while the app is quitting / the scene is being torn down, where an async
            // continuation may never run. Write synchronously when possible; async only as a fallback.
            var dirtyPackages = _packages
                .AsValueEnumerable()
                .Where(package => package.IsDirty &&
                                  !(package is ISyncPersistencePackage sync && sync.TrySaveImmediate()))
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

            // Anyone still waiting is released; the next initialization gets a new signal.
            _readySource.TrySetCanceled();
            _readySource = new UniTaskCompletionSource();
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
        /// If strategy is OnAppClose or Debounced, auto-registers to SaveLifecycleHook (flushed on pause / quit).
        /// </summary>
        /// <remarks>
        /// Packages registered during bootstrap are queued and initialized by the active bootstrap pass.
        /// Packages registered after bootstrap still initialize asynchronously.
        /// Returning from this method does not guarantee that the package has finished loading its persisted data.
        /// </remarks>
        public void RegisterPackage(IPersistencePackage package)
        {
            if (!TryAddPackage(package)) return;
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
            // Copy: a save may cause (un)registration.
            foreach (var package in new List<IPersistencePackage>(_packages))
            {
                if (package.IsDirty) await package.SaveNowAsync();
            }
        }

        /// <summary>
        /// Reload all packages from storage.
        /// </summary>
        /// <remarks>
        /// This method triggers <see cref="LoadAllAsync"/> via <c>Forget()</c> and returns immediately.
        /// Use <see cref="LoadAllAsync"/> when the caller must await completion.
        /// </remarks>
        public void LoadAll(bool discardUnsavedChanges = false)
        {
            LoadAllAsync(discardUnsavedChanges).Forget();
        }

        /// <summary>
        /// Reloads every package. Packages with unsaved changes are skipped (with a warning) unless
        /// <paramref name="discardUnsavedChanges"/> is true.
        /// </summary>
        public async UniTask LoadAllAsync(bool discardUnsavedChanges = false)
        {
            // Copy: a load may cause (un)registration.
            foreach (var package in new List<IPersistencePackage>(_packages))
            {
                if (package.IsDirty && !discardUnsavedChanges)
                {
                    this.LogWarning($"Skipped reloading '{package.StorageKey}': it has unsaved changes.");
                    continue;
                }

                await package.LoadAsync();
            }
        }

        /// <summary>
        /// Completes when the initially configured packages finished loading. Throws if the initialization failed
        /// (e.g. no save service), and is canceled if the manager is torn down first.
        /// </summary>
        public UniTask WaitUntilReadyAsync(CancellationToken cancellationToken = default)
        {
            return cancellationToken.CanBeCanceled
                ? _readySource.Task.AttachExternalCancellation(cancellationToken)
                : _readySource.Task;
        }

        /// <summary>
        /// Gets whether the initially configured package set has completed asynchronous initialization.
        /// </summary>
        /// <remarks>
        /// This flag does not mean that every package ever registered with the manager is fully loaded.
        /// Packages added later through <see cref="RegisterPackage"/> may still be loading after this becomes true.
        /// </remarks>
        public override bool IsInitialized => _initialized;

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

            // Exact type first, then the first package assignable to T (base class or interface).
            var package = _packages.AsValueEnumerable().FirstOrDefault(p => p.GetType() == type)
                          ?? _packages.AsValueEnumerable().FirstOrDefault(p => p is T);
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
