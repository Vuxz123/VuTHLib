using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Singleton that handles mobile app lifecycle events.
    /// Forces save on all dirty packages when app is paused or backgrounded.
    /// Registered via VContainer IStartable.
    /// </summary>
    public class SaveLifecycleHook : IInitializable, IDisposable
    {
        private static readonly List<IPersistencePackage> _packages = new();
        
        [Inject]
        public void Initialize()
        {
            // Register to Unity lifecycle events
            Application.focusChanged += OnFocusChanged;
            //Application. += OnPause;
            Application.quitting += OnQuitting;
            
            Debug.Log("[SaveLifecycleHook] Initialized");
        }
        
        /// <summary>
        /// Register a package to be managed by lifecycle hook.
        /// </summary>
        public static void RegisterPackage(IPersistencePackage package)
        {
            if (!_packages.Contains(package))
            {
                _packages.Add(package);
                Debug.Log($"[SaveLifecycleHook] Registered package: {package.StorageKey}");
            }
        }
        
        /// <summary>
        /// Unregister a package from lifecycle hook.
        /// </summary>
        public static void UnregisterPackage(IPersistencePackage package)
        {
            _packages.Remove(package);
        }
        
        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                // App lost focus - save all dirty packages
                SaveAllDirty();
            }
        }
        
        private void OnPause(bool paused)
        {
            if (paused)
            {
                // App was paused (e.g., user pressed home button on Android)
                Debug.Log("[SaveLifecycleHook] App paused, saving dirty packages...");
                SaveAllDirty();
            }
        }
        
        private void OnQuitting()
        {
            // App is about to quit
            Debug.Log("[SaveLifecycleHook] App quitting, saving dirty packages...");
            SaveAllDirty();
        }
        
        private void SaveAllDirty()
        {
            foreach (var package in _packages)
            {
                if (package.Strategy == SaveStrategy.OnAppClose || package.Strategy == SaveStrategy.ManualOnly)
                {
                    // Only save packages that should save on app close
                    if (package.IsDirty)
                    {
                        Debug.Log($"[SaveLifecycleHook] Saving dirty package: {package.StorageKey}");
                        package.SaveNow();
                    }
                }
            }
        }
        
        public void Dispose()
        {
            Application.focusChanged -= OnFocusChanged;
            //Application.pause -= OnPause;
            Application.quitting -= OnQuitting;
            
            _packages.Clear();
            
            Debug.Log("[SaveLifecycleHook] Disposed");
        }
    }
}
