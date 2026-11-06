using System;
using System.Collections.Generic;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if VCONTAINER
using VContainer;
using VContainer.Unity;
#endif

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
    /// Packages whose backend has no synchronous path (no <c>ISyncSaveBackend</c>) get an async save on pause, which
    /// only completes if the app resumes, and are reported as not saved on quit. The manager warns about them at
    /// startup.
    /// <para>
    /// With VContainer it is an entry point; without it, <see cref="DataPersistenceManager"/> creates and initializes its
    /// own, so pause / quit flushes work either way.
    /// </para>
    /// </summary>
    public class SaveLifecycleHook : IDisposable
#if VCONTAINER
        , IInitializable
#endif
    {
        private readonly HashSet<IPersistencePackage> _packages = new();
        private ApplicationPauseRelay _pauseRelay;
        private bool _quitHandled;
        private bool _initialized;

#if VCONTAINER
        [Inject]
#endif
        public SaveLifecycleHook()
        {
        }

        /// <summary>Subscribes to focus / pause / quit. Called once (VContainer entry point, or the manager).</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

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
        /// Synchronously flushes every registered dirty package, and only that: nothing is saved asynchronously. The
        /// caller decides what to do with the packages that could not be written synchronously (see
        /// <see cref="FlushReport.Unsupported"/>). Public so games can call it from their own "about to be killed" paths
        /// (e.g. before a forced restart).
        /// </summary>
        public FlushReport FlushAllImmediate() => Flush(new List<IPersistencePackage>(_packages)); // copy: a save may (un)register

        /// <summary>Saves each dirty package synchronously and sorts the outcomes.</summary>
        internal static FlushReport Flush(IEnumerable<IPersistencePackage> packages)
        {
            var written = 0;
            List<IPersistencePackage> unsupported = null;
            List<IPersistencePackage> failed = null;

            foreach (var package in packages)
            {
                if (!package.IsDirty) continue;

                var result = package is ISyncPersistencePackage sync ? sync.SaveImmediate() : SyncSaveResult.Unsupported;
                switch (result)
                {
                    case SyncSaveResult.Written:
                        written++;
                        break;
                    case SyncSaveResult.Unsupported:
                        (unsupported ??= new List<IPersistencePackage>()).Add(package);
                        break;
                    case SyncSaveResult.Failed:
                        (failed ??= new List<IPersistencePackage>()).Add(package);
                        break;
                    // NotNeeded, Loading, Blocked, NotReady: nothing to write, on purpose.
                }
            }

            return new FlushReport(written, unsupported, failed);
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

            // Best effort for packages without a sync path: the save only finishes if the app resumes. Said, not hidden.
            foreach (var package in FlushAllImmediate().Unsupported)
            {
                this.LogWarning($"'{package.StorageKey}' is saved asynchronously on pause (its backend has no " +
                                "synchronous save path): it completes only if the app resumes.");
                package.SaveNowAsync().Forget();
            }
        }

        private void OnQuitting()
        {
            if (_quitHandled) return;
            _quitHandled = true;

            this.Log("App quitting, flushing dirty packages...");

            // Unity does not run async continuations while quitting: an async save would never complete.
            foreach (var package in FlushAllImmediate().Unsupported)
            {
                this.LogError($"'{package.StorageKey}' was NOT saved on quit: its backend has no synchronous save path.");
            }
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
            _initialized = false;
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

    /// <summary>The outcome of a synchronous flush (<see cref="SaveLifecycleHook.FlushAllImmediate"/>).</summary>
    public readonly struct FlushReport
    {
        private static readonly IReadOnlyList<IPersistencePackage> None = Array.Empty<IPersistencePackage>();

        private readonly IReadOnlyList<IPersistencePackage> _unsupported;
        private readonly IReadOnlyList<IPersistencePackage> _failed;

        public FlushReport(int written, IReadOnlyList<IPersistencePackage> unsupported, IReadOnlyList<IPersistencePackage> failed)
        {
            Written = written;
            _unsupported = unsupported;
            _failed = failed;
        }

        /// <summary>Packages written synchronously.</summary>
        public int Written { get; }

        /// <summary>
        /// Dirty packages whose backend has no synchronous path: not saved. Only an async save can still write them,
        /// and it completes only if the app keeps running.
        /// </summary>
        public IReadOnlyList<IPersistencePackage> Unsupported => _unsupported ?? None;

        /// <summary>
        /// Packages whose write threw: already reported through <see cref="IPersistencePackage.SaveFailed"/>, which
        /// scheduled a retry. Do not save them again.
        /// </summary>
        public IReadOnlyList<IPersistencePackage> Failed => _failed ?? None;
    }
}
