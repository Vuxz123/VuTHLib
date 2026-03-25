using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Persistant.SaveSystem;
using JetBrains.Annotations;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ZLinq;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Manager that orchestrates persistence packages and save system.
    /// Supports both VContainer DI and non-VContainer modes via VCONTAINER macro.
    /// </summary>
    public class DataPersistenceManager : VBootstrapManager<DataPersistenceManager, IDataPersistenceManager>, IDataPersistenceManager
    {
#if VCONTAINER
        private IObjectResolver _container;
#endif
        
        [CanBeNull] private ISaveService _saveService;
        
        private readonly List<IPersistencePackage> _packages = new();
        
        #region VContainer DI
        
#if VCONTAINER
        [Inject]
        public void Construct(IObjectResolver container, ISaveManager saveManager)
        {
            _container = container;
            _saveService = saveManager;
            
            if (_saveService == null)
            {
                Debug.LogError("[DataPersistenceManager] ISaveManager does not implement ISaveService!");
            }
        }
        
        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            builder.RegisterComponent(this).AsImplementedInterfaces();
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
            // Try to get SaveServiceManager singleton
            if (SaveServiceManager.HasInstance)
            {
                _saveService = SaveServiceManager.Instance;
            }
            
            if (_saveService == null)
            {
                this.LogError("Cannot find ISaveService! Make sure SaveServiceManager is initialized.");
                return;
            }
            
            InitializePackages();
        }
        
        private void InitializePackages()
        {
            foreach (var package in _packages)
            {
                if (_saveService == null) continue;
                // Use reflection to call Initialize since it's a generic method
                var initializeMethod = package.GetType().GetMethod("Initialize");
                initializeMethod?.Invoke(package, new object[] { _saveService });
                    
                // Call Load
                package.Load();
            }
            
            this.Log($"Initialized {_packages.Count} persistence packages");
        }
        
        protected override void DeinitializeBootstrap()
        {
            // Force save all dirty packages
            foreach (var package in _packages)
            {
                if (package.IsDirty)
                {
                    package.SaveNow();
                }
            }
            
            _packages.Clear();
            this.Log("DataPersistenceManager deinitialized.");
        }
        
        #endregion
        
        #region Package Management
        
        /// <summary>
        /// Register a persistence package to be managed.
        /// Call this in your package constructor or DI setup.
        /// </summary>
        public void RegisterPackage(IPersistencePackage package)
        {
            if (_packages.Contains(package)) return;
            _packages.Add(package);
            this.Log($"Registered package: {package.StorageKey}");
                
            // If already initialized, initialize the new package
            if (_saveService == null) return;
            var initializeMethod = package.GetType().GetMethod("Initialize");
            initializeMethod?.Invoke(package, new object[] { _saveService });
        }
        
        /// <summary>
        /// Unregister a persistence package.
        /// </summary>
        public void UnregisterPackage(IPersistencePackage package)
        {
            if (!_packages.Remove(package)) return;
            if (package.IsDirty)
            {
                package.SaveNow();
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
                package.SaveNow();
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
        public bool IsInitialized => _saveService != null;
        
        /// <summary>
        /// Get the number of registered packages.
        /// </summary>
        public int PackageCount => _packages.Count;
        
        #endregion
    }
}
