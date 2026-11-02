using System;
using System.Collections.Generic;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Flushes registered dirty packages on app lifecycle events.
    /// <list type="bullet">
    /// <item><b>Focus lost</b> → async save (cheap, the app keeps running).</item>
    /// <item><b>Pause</b> (home button / app switch on mobile) → <b>synchronous</b> save: the OS may kill the
    /// process right after this callback without ever raising <c>Application.quitting</c>.</item>
    /// <item><b>Quit</b> → <b>synchronous</b> save: Unity does not wait for async continuations while quitting.</item>
    /// </list>
    /// Packages that cannot save synchronously (custom backend without <c>ISyncSaveBackend</c>) fall back to a
    /// best-effort async save.
    /// </summary>
    public class SaveLifecycleHook : IInitializable, IDisposable
    {
        private readonly HashSet<IPersistencePackage> _packages = new();
        private ApplicationPauseRelay _pauseRelay;
        private bool _quitHandled;

        [Inject]
        public SaveLifecycleHook()
        {
        }

        public void Initialize()
        {
            Application.focusChanged += OnFocusChanged;
            Application.quitting += OnQuitting;

            _pauseRelay = ApplicationPauseRelay.Create();
            _pauseRelay.PauseChanged += OnPauseChanged;

            this.Log("SaveLifecycleHook initialized");
        }

        /// <summary>
        /// Register a package to be flushed on pause / quit / focus loss.
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

        /// <summary>
        /// Synchronously flush every registered dirty package. Public so games can call it from their own
        /// "about to be killed" paths (e.g. before a forced restart).
        /// </summary>
        /// <returns>Number of packages written synchronously.</returns>
        public int FlushAllImmediate()
        {
            var written = 0;

            // Copy: a save may cause (un)registration through the manager.
            foreach (var package in new List<IPersistencePackage>(_packages))
            {
                if (!package.IsDirty) continue;

                if (package is ISyncPersistencePackage sync && sync.TrySaveImmediate())
                {
                    written++;
                    continue;
                }

                // No synchronous path available — best effort.
                this.LogWarning($"Package '{package.StorageKey}' has no synchronous save path; falling back to async.");
                package.SaveNow();
            }

            return written;
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

        private void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus) return;
            SaveAllDirtyAsync().Forget();
        }

        private void OnPauseChanged(bool paused)
        {
            if (!paused) return;
            this.Log("App paused, flushing dirty packages...");
            FlushAllImmediate();
        }

        private void OnQuitting()
        {
            if (_quitHandled) return;
            _quitHandled = true;

            this.Log("App quitting, flushing dirty packages...");
            FlushAllImmediate();
        }

        private async UniTask SaveAllDirtyAsync()
        {
            foreach (var package in new List<IPersistencePackage>(_packages))
            {
                if (!package.IsDirty) continue;
                this.Log($"Saving dirty package: {package.StorageKey}");
                await package.SaveNowAsync();
            }
        }

        public void Dispose()
        {
            Application.focusChanged -= OnFocusChanged;
            Application.quitting -= OnQuitting;

            if (_pauseRelay != null)
            {
                _pauseRelay.PauseChanged -= OnPauseChanged;
                if (Application.isPlaying) UnityEngine.Object.Destroy(_pauseRelay.gameObject);
                else UnityEngine.Object.DestroyImmediate(_pauseRelay.gameObject);
                _pauseRelay = null;
            }

            _packages.Clear();

            this.Log("SaveLifecycleHook disposed");
        }
    }
}
