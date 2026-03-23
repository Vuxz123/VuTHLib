#nullable enable
using System;
using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Persistant.SaveSystem;
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
    public class DataPersistenceManager : VBootstrapManager<DataPersistenceManager, IDataPersistenceManager>, IDataPersistenceManager
    {
        private ISaveService? _saveService;
        private SaveLifecycleHook? _lifecycleHook;

        private readonly List<IPersistencePackage> _configuredPackages = new();
        private readonly List<IPersistencePackage> _packages = new();
        private readonly Dictionary<IPersistencePackage, IDisposable> _saveSubscriptions = new();
        private bool _configuredPackagesLoaded;
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

            InitializePackages(packages);
        }
        
        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            foreach (var package in GetConfiguredPackages())
            {
                builder.RegisterInstance(package).As<IPersistencePackage>();
            }

            builder.RegisterComponent(this).AsImplementedInterfaces();
            builder.Register<SaveLifecycleHook>(Lifetime.Singleton).AsImplementedInterfaces();
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

            InitializePackages(GetConfiguredPackages());
        }

        private void InitializePackages(IReadOnlyList<IPersistencePackage> initialPackages)
        {
            if (_initialized) return;
            _initialized = true;

            foreach (var package in initialPackages)
            {
                if (_packages.Contains(package)) continue;
                _packages.Add(package);
            }

            foreach (var package in _packages)
            {
                SetupSavePipeline(package);

                if (_saveService == null) continue;
                package.SetSaveService(_saveService);
                package.Load();
            }
            
            this.Log($"Initialized {_packages.Count} persistence packages");
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
            // Force save all dirty packages
            foreach (var package in _packages.AsValueEnumerable().Where(package => package.IsDirty))
            {
                package.SaveNowAsync().GetAwaiter().GetResult();
            }
            
            // Dispose all subscriptions
            foreach (var kvp in _saveSubscriptions)
            {
                kvp.Value.Dispose();
            }
            _saveSubscriptions.Clear();

            _packages.Clear();
            this.Log("DataPersistenceManager deinitialized.");
        }

        #endregion

        #region Package Management

        /// <summary>
        /// Register a persistence package to be managed.
        /// If strategy is OnAppClose or ManualOnly, auto-registers to SaveLifecycleHook.
        /// </summary>
        public void RegisterPackage(IPersistencePackage package)
        {
            if (_packages.Contains(package)) return;
            _packages.Add(package);
            this.Log($"Registered package: {package.StorageKey}");

            // If already initialized, setup save pipeline and initialize
            if (!_initialized) return;
            SetupSavePipeline(package);

            if (_saveService == null) return;
            package.SetSaveService(_saveService);
            package.Load();
        }

        /// <summary>
        /// Unregister a persistence package.
        /// </summary>
        public void UnregisterPackage(IPersistencePackage package)
        {
            if (!_packages.Remove(package)) return;

            if (_saveSubscriptions.TryGetValue(package, out var sub))
            {
                sub.Dispose();
                _saveSubscriptions.Remove(package);
            }
            
            if (package.IsDirty)
            {
                package.SaveNowAsync().GetAwaiter().GetResult();
            }
            this.Log($"Unregistered package: {package.StorageKey}");
        }

        /// <summary>
        /// Force save all dirty packages.
        /// </summary>
        public void SaveAll()
        {
            foreach (var package in _packages.AsValueEnumerable().Where(package => package.IsDirty))
            {
                package.SaveNowAsync().GetAwaiter().GetResult();
            }

            this.Log("Saved all dirty packages");
        }

        /// <summary>
        /// Load all packages from storage.
        /// </summary>
        public void LoadAll()
        {
            foreach (var package in _packages)
            {
                package.Load();
            }
            this.Log("Loaded all packages");
        }

        /// <summary>
        /// Check if manager is initialized.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Get the number of registered packages.
        /// </summary>
        public int PackageCount => _packages.Count;

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
