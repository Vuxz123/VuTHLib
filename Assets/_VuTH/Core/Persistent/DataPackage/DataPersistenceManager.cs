#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Persistent.SaveSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZLinq;
#if VCONTAINER
using VContainer;
using VContainer.Unity;
#endif

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
        private bool _ownsLifecycleHook;
        private readonly SemaphoreSlim _slotGate = new(1, 1);
        private IReadOnlyList<IPersistencePackage>? _injectedPackages;

        private readonly List<IPersistencePackage> _configuredPackages = new();
        private readonly PackageRegistry _registry = new();
        private readonly PackageSavePipeline _savePipeline = new();
        private readonly HashSet<IPersistencePackage> _initializedPackages = new();
        private readonly HashSet<IPersistencePackage> _initializingPackages = new();
        private CancellationTokenSource? _initializationCts;
        private UniTaskCompletionSource _readySource = new();
        private string? _activeSlot;
        private bool _configuredPackagesLoaded;
        // Created by this manager from the profile, disposed by it on deinitialization (no one else owns them).
        private readonly HashSet<IPersistencePackage> _retiredPackages = new();
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
            var configured = GetConfiguredPackages();
            foreach (var package in configured)
            {
                // Also as its concrete type, so consumers can inject e.g. PlayerProfilePackage directly.
                builder.RegisterInstance(package).AsSelf();
            }

            // Created by the profile (Activator), not by the container: inject their [Inject] members once it is built.
            builder.RegisterBuildCallback(container =>
            {
                foreach (var package in configured) container.Inject(package);
            });

            RegisterSelf(builder);
            RegisterLifecycleHook(builder);
        }

        /// <summary>
        /// Registers <see cref="SaveLifecycleHook"/> as an entry point, so VContainer initializes it (subscribing to
        /// focus / pause / quit) and disposes it with the scope.
        /// <para>
        /// The injected hook takes precedence: <see cref="Construct"/> runs before <see cref="InitializeBootstrap"/>, so
        /// <c>EnsureLifecycleHook</c> finds it and creates none. The manager only creates (and disposes) its own hook
        /// when nothing injected one, e.g. without VContainer.
        /// </para>
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

            // Without DI (or when the manager is not created through the root scope) nothing provides the hook:
            // create one, otherwise OnAppClose / Debounced packages are never flushed on pause / quit.
            EnsureLifecycleHook();

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
                Fail("Cannot find ISaveService! Make sure SaveServiceManager is initialized.",
                     _injectedPackages ?? GetConfiguredPackages());
                return;
            }

            BeginInitializePackages(GetConfiguredPackages());
        }

        private void EnsureLifecycleHook()
        {
            if (_lifecycleHook != null) return;

            _lifecycleHook = new SaveLifecycleHook();
            _lifecycleHook.Initialize();
            _ownsLifecycleHook = true;
        }

        /// <summary>Reports that packages cannot load, releasing whoever waits for them or for the manager.</summary>
        private void Fail(string error, IEnumerable<IPersistencePackage> packages)
        {
            this.LogError(error);
            var exception = new InvalidOperationException(error);

            foreach (var package in packages)
            {
                if (package is IPackageLoadFailure failure) failure.FailLoading(exception);
            }

            _readySource.TrySetException(exception);
        }

        private void BeginInitializePackages(IReadOnlyList<IPersistencePackage> initialPackages)
        {
            var replaced = false;
            foreach (var package in initialPackages)
            {
                // Disposed by an earlier deinitialization: replaced by new instances from the profile.
                if (_retiredPackages.Contains(package))
                {
                    replaced = true;
                    continue;
                }

                TryAddPackage(package);
            }

            if (replaced && !ReferenceEquals(initialPackages, _configuredPackages))
            {
                foreach (var package in GetConfiguredPackages()) TryAddPackage(package);
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
                    Fail(error, _registry.Snapshot());
                    return;
                }

                while (_registry.TryGetFirstExcept(
                           p => _initializedPackages.Contains(p) || _initializingPackages.Contains(p), out var package))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await InitializePackageAsync(package, cancellationToken);
                }

                _initialized = true;
                _readySource.TrySetResult();
                this.Log($"Initialized {_registry.Count} persistence packages");
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

        /// <summary>Adds a package (in the active slot) unless it is already managed or its storage key is taken.</summary>
        private bool TryAddPackage(IPersistencePackage package)
        {
            if (_registry.Contains(package)) return false;

            if (package is not IManagedPersistencePackage managed)
            {
                this.LogError($"Package {package.GetType().Name} is not registered: derive it from PersistencePackage<TData>.");
                return false;
            }

            // Checked before SetSlot, which resets the package's data: a rejected package must stay untouched.
            var key = package.IsSlotScoped ? SaveSlots.GetKey(_activeSlot, package.BaseStorageKey) : package.StorageKey;
            var owner = _registry.FindByKey(key);
            if (owner != null)
            {
                this.LogError($"Package {package.GetType().Name} uses the storage key '{key}' of {owner.GetType().Name}; " +
                              "it is not registered, as they would overwrite each other's save.");
                return false;
            }

            if (package.IsSlotScoped) managed.SetSlot(_activeSlot);
            if (!_registry.TryAdd(package, out var error))
            {
                if (error != null) this.LogError(error);
                return false;
            }

            package.SaveBlocked += OnPackageSaveBlocked;
            package.SaveFailed += OnPackageSaveFailed;
            return true;
        }

        private static IManagedPersistencePackage Managed(IPersistencePackage package) => (IManagedPersistencePackage)package;

        private void OnPackageSaveFailed(IPersistencePackage package, Exception error)
        {
            try
            {
                OnSaveFailed?.Invoke(package, error);
            }
            catch (Exception e)
            {
                this.LogError($"An OnSaveFailed subscriber threw: {e}");
            }
        }

        /// <inheritdoc />
        public event Action<IPersistencePackage, Exception>? OnSaveFailed;

        private void OnPackageSaveBlocked(IPersistencePackage package)
        {
            try
            {
                OnSaveBlocked?.Invoke(package);
            }
            catch (Exception e)
            {
                this.LogError($"An OnSaveBlocked subscriber threw: {e}");
            }
        }

        /// <inheritdoc />
        public event Action<IPersistencePackage>? OnSaveBlocked;

        private async UniTask InitializePackageAsync(IPersistencePackage package, CancellationToken cancellationToken)
        {
            if (_initializedPackages.Contains(package) ||
                _initializingPackages.Contains(package) ||
                !_registry.Contains(package))
            {
                return;
            }

            _initializingPackages.Add(package);

            try
            {
                _savePipeline.Setup(package, _lifecycleHook);

                if (_saveService == null)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                Managed(package).SetSaveService(_saveService);
                WarnIfNotSavedOnClose(package);
                await package.LoadAsync();

                if (!_registry.Contains(package))
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

        /// <summary>
        /// Saved on pause / quit only works synchronously: say so at startup, not when the data is already lost.
        /// </summary>
        private void WarnIfNotSavedOnClose(IPersistencePackage package)
        {
            if (package.Strategy is not (SaveStrategy.OnAppClose or SaveStrategy.Debounced)) return;
            if (_saveService is ISyncSaveService { SupportsSyncSave: true }) return;

            this.LogWarning($"'{package.StorageKey}' ({package.Strategy}) is flushed on pause / quit, but the save " +
                            "backend has no synchronous path (ISyncSaveBackend): changes still pending then are likely " +
                            (package.Strategy == SaveStrategy.OnAppClose ? "LOST. " : "lost. ") +
                            "Use a backend implementing ISyncSaveBackend, or the Immediate strategy.");
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

        protected override void DeinitializeBootstrap()
        {
            // Deinit usually runs while the app is quitting / the scene is being torn down, where an async
            // continuation may never run. Write synchronously; async only for packages without a sync path (a failed
            // write already scheduled its retry, a blocked or loading package has nothing to write).
            var dirtyPackages = SaveLifecycleHook.Flush(_registry.Snapshot()).Unsupported;

            // The packages this manager created are disposed once flushed (their fields' subscriptions with them).
            var owned = new List<IPersistencePackage>(_configuredPackages);
            _retiredPackages.UnionWith(owned);
            _configuredPackages.Clear();
            _configuredPackagesLoaded = false;
            FlushThenDisposeAsync(dirtyPackages, owned).Forget();

            _savePipeline.Clear();

            foreach (var package in _registry.Snapshot())
            {
                package.SaveBlocked -= OnPackageSaveBlocked;
                package.SaveFailed -= OnPackageSaveFailed;
            }

            if (_ownsLifecycleHook)
            {
                _lifecycleHook?.Dispose();
                _lifecycleHook = null;
                _ownsLifecycleHook = false;
            }

            _initializationCts?.Cancel();
            _initializationCts?.Dispose();
            _initializationCts = null;

            _registry.Clear();
            _initializedPackages.Clear();
            _initializingPackages.Clear();
            _initializationStarted = false;
            _initialized = false;

            // Anyone still waiting is released; the next initialization gets a new signal.
            _readySource.TrySetCanceled();
            _readySource = new UniTaskCompletionSource();
            this.Log("DataPersistenceManager deinitialized.");
        }

        private async UniTask FlushThenDisposeAsync(IReadOnlyList<IPersistencePackage> dirty,
            IReadOnlyList<IPersistencePackage> owned)
        {
            try
            {
                foreach (var package in dirty)
                {
                    await package.SaveNowAsync();
                }
            }
            finally
            {
                foreach (var package in owned)
                {
                    try
                    {
                        (package as IDisposable)?.Dispose();
                    }
                    catch (Exception e)
                    {
                        this.LogError($"Disposing package '{package.StorageKey}' threw: {e}");
                    }
                }
            }
        }

        #endregion

        #region Package Management

        /// <summary>
        /// Register a persistence package to be managed.
        /// If strategy is OnAppClose or Debounced, auto-registers to SaveLifecycleHook (flushed on pause / quit).
        /// A slot-scoped package is put in the active slot.
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
            if (!_registry.Remove(package)) return;
            package.SaveBlocked -= OnPackageSaveBlocked;
            package.SaveFailed -= OnPackageSaveFailed;

            _initializedPackages.Remove(package);
            _initializingPackages.Remove(package);
            _savePipeline.Remove(package);

            SavePackageBeforeUnregisterAsync(package).Forget();
            this.Log($"Unregistered package: {package.StorageKey}");
        }

        private static async UniTask SavePackageBeforeUnregisterAsync(IPersistencePackage package)
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
            foreach (var package in _registry.Snapshot())
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
            foreach (var package in _registry.Snapshot())
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
        public int PackageCount => _registry.Count;

        #endregion

        #region Save Slots

        /// <inheritdoc />
        public string? ActiveSlot => _activeSlot;

        /// <inheritdoc />
        /// <remarks>
        /// Switches run one after another. Cancellation is only honored before the switch starts: once started, every
        /// slot-scoped package moves to the new slot, so they never end up split between two slots.
        /// </remarks>
        public async UniTask SwitchSlotAsync(string? slot, CancellationToken cancellationToken = default)
        {
            await _slotGate.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                slot = string.IsNullOrEmpty(slot) ? null : slot;
                if (slot == _activeSlot) return;

                // From here on the switch always completes. A package registered meanwhile joins the new slot.
                _activeSlot = slot;
                var scoped = _registry.Snapshot().AsValueEnumerable().Where(p => p.IsSlotScoped).ToList();
                var loads = new List<UniTask>(scoped.Count);

                foreach (var package in scoped)
                {
                    // Pending changes belong to the slot being left...
                    await SaveUntilCleanAsync(package);

                    // ...and the package moves right after, with no await in between: no change can slip into the gap.
                    var managed = Managed(package);
                    managed.SetSlot(slot);
                    if (_saveService != null) managed.SetSaveService(_saveService);
                    loads.Add(package.LoadAsync()); // marks it loading at once: later changes wait for the new data
                }

                await UniTask.WhenAll(loads);
                this.Log($"Switched to save slot '{slot ?? "(default)"}'.");
            }
            finally
            {
                _slotGate.Release();
            }
        }

        /// <summary>Saves until nothing is pending (a change may arrive during the save); gives up after a few failures.</summary>
        private async UniTask SaveUntilCleanAsync(IPersistencePackage package)
        {
            for (var attempt = 0; attempt < 3 && package.IsDirty && package.CanSave; attempt++)
            {
                await package.SaveNowAsync();
            }

            if (package.IsDirty)
            {
                this.LogWarning($"Unsaved changes of '{package.StorageKey}' are dropped when leaving its slot: " +
                                (package.CanSave ? "the save failed." : "saving it is disabled."));
            }
        }

        /// <inheritdoc />
        public async UniTask DeleteSlotAsync(string? slot)
        {
            await _slotGate.WaitAsync();
            try
            {
                await DeleteSlotCoreAsync(slot);
            }
            finally
            {
                _slotGate.Release();
            }
        }

        private async UniTask DeleteSlotCoreAsync(string? slot)
        {
            slot = string.IsNullOrEmpty(slot) ? null : slot;
            var saveService = _saveService ?? throw new InvalidOperationException("No save service.");

            await DeleteEachAsync(_registry.Snapshot().AsValueEnumerable().Where(p => p.IsSlotScoped).ToList(), package =>
            {
                if (slot != _activeSlot) return saveService.DeleteAsync(SaveSlots.GetKey(slot, package.BaseStorageKey));

                Managed(package).SetSaveService(saveService);
                return package.DeleteAsync();
            });
        }

        /// <inheritdoc />
        public async UniTask DeleteAllAsync()
        {
            var saveService = _saveService ?? throw new InvalidOperationException("No save service.");

            // Not during a slot switch: packages would be deleted in one slot and reset in the other.
            await _slotGate.WaitAsync();
            try
            {
                await DeleteEachAsync(_registry.Snapshot(), package =>
                {
                    Managed(package).SetSaveService(saveService);
                    return package.DeleteAsync();
                });
            }
            finally
            {
                _slotGate.Release();
            }
        }

        /// <summary>
        /// Deletes every package even if some fail (a package that cannot create its defaults deletes nothing), then
        /// throws the failures together.
        /// </summary>
        private async UniTask DeleteEachAsync(IReadOnlyList<IPersistencePackage> packages,
            Func<IPersistencePackage, UniTask> delete)
        {
            List<Exception>? errors = null;
            foreach (var package in packages)
            {
                try
                {
                    await delete(package);
                }
                catch (Exception e)
                {
                    this.LogError($"Could not delete '{package.StorageKey}': {e.Message}");
                    (errors ??= new List<Exception>()).Add(e);
                }
            }

            if (errors != null)
                throw new AggregateException($"{errors.Count} of {packages.Count} package(s) could not be deleted.", errors);
        }

        #endregion

        #region Package Retrieval

        public T? GetPackage<T>() where T : class, IPersistencePackage
        {
            var package = _registry.Find<T>();
            if (package == null) this.LogWarning($"Requested package of type {typeof(T).Name} not found.");
            return package;
        }

        public bool TryGetPackage<T>(out T? package) where T : class, IPersistencePackage
        {
            package = _registry.Find<T>(); // a Try method: not finding it is an expected outcome, no warning
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
    }
}
