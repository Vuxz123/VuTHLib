using System;
using System.Collections.Generic;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ZLinq;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Handles mobile app lifecycle events for persistence.
    /// Forces save on all dirty packages when app loses focus, pauses, or quits.
    /// Registered via VContainer IStartable.
    /// Uses instance-based package list (not static) for proper DI and testability.
    /// </summary>
    public class SaveLifecycleHook : IInitializable, IDisposable
    {
        private readonly HashSet<IPersistencePackage> _packages = new();
        private bool _quitRequested;

        [Inject]
        public SaveLifecycleHook()
        {
        }

        public void Initialize()
        {
            // Register to Unity lifecycle events
            Application.focusChanged += OnFocusChanged;
            //Application.pausedChanged += OnPausedChanged;
            Application.quitting += OnQuitting;
            
            this.Log("SaveLifecycleHook initialized");
        }

        /// <summary>
        /// Register a package to be managed by lifecycle hook.
        /// </summary>
        public IDisposable RegisterPackage(IPersistencePackage package)
        {
            _packages.Add(package);
            return new LifecycleRegistration(_packages, package);
        }

        /// <summary>
        /// Unregister a package from lifecycle hook.
        /// </summary>
        public void UnregisterPackage(IPersistencePackage package)
        {
            _packages.Remove(package);
        }

        private sealed class LifecycleRegistration : IDisposable
        {
            private readonly HashSet<IPersistencePackage> _packages;
            private readonly IPersistencePackage _package;
            private bool _disposed;

            public LifecycleRegistration(HashSet<IPersistencePackage> packages, IPersistencePackage package)
            {
                _packages = packages;
                _package = package;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _packages.Remove(_package);
            }
        }

        private async void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                // App lost focus (switched away) — save all dirty packages
                await SaveAllDirtyAsync();
            }
        }

        // private void OnPausedChanged(bool paused)
        // {
        //     if (paused)
        //     {
        //         // App was paused (user pressed home button on mobile, etc.)
        //         this.Log("App paused, saving dirty packages...");
        //         SaveAllDirty();
        //     }
        // }

        private async void OnQuitting()
        {
            if (_quitRequested) return;
            _quitRequested = true;
            
            this.Log("App quitting, saving dirty packages...");
            await SaveAllDirtyAsync();
        }

        private async UniTask SaveAllDirtyAsync()
        {
            foreach (var package in _packages.AsValueEnumerable()
                         .Where(package => package.IsDirty &&
                                           package.Strategy 
                                               is SaveStrategy.OnAppClose 
                                               or SaveStrategy.ManualOnly))
            {
                this.Log($"Saving dirty package: {package.StorageKey}");
                await package.SaveNowAsync();
            }
        }

        public void Dispose()
        {
            Application.focusChanged -= OnFocusChanged;
            //Application.pausedChanged -= OnPausedChanged;
            Application.quitting -= OnQuitting;
            
            _packages.Clear();
            
            this.Log("SaveLifecycleHook disposed");
        }
    }
}
