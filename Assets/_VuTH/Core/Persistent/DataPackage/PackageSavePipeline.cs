#nullable enable
using System;
using System.Collections.Generic;
using R3;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Saves each package according to its <see cref="SaveStrategy"/>: subscribes to its dirty state, and registers it
    /// with the <see cref="SaveLifecycleHook"/> when pending changes must be flushed on pause / quit.
    /// </summary>
    internal sealed class PackageSavePipeline
    {
        private readonly Dictionary<IPersistencePackage, IDisposable> _subscriptions = new();

        /// <summary>(Re)creates the save triggers of <paramref name="package"/>.</summary>
        public void Setup(IPersistencePackage package, SaveLifecycleHook? lifecycleHook)
        {
            Remove(package);

            var group = new SubscriptionGroup();

            switch (package.Strategy)
            {
                case SaveStrategy.Immediate:
                    group.Add(package.DirtyObservable
                        .Where(dirty => dirty)
                        .Subscribe(_ => package.SaveNow()));
                    break;

                case SaveStrategy.Debounced:
                    group.Add(package.DirtyObservable
                        .Where(dirty => dirty)
                        // Explicit Unity time (main thread, ignoring timeScale): a paused game still saves.
                        .ThrottleLast(TimeSpan.FromSeconds(package.DebounceSeconds), UnityTimeProvider.UpdateIgnoreTimeScale)
                        .Subscribe(_ => package.SaveNow()));
                    // Changes still waiting for the debounce window would be lost if the app is paused/killed.
                    if (lifecycleHook != null) group.Add(lifecycleHook.RegisterPackage(package));
                    break;

                case SaveStrategy.ManualOnly:
                    break;

                case SaveStrategy.OnAppClose:
                    if (lifecycleHook != null) group.Add(lifecycleHook.RegisterPackage(package));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(package), package.Strategy, "Unknown save strategy.");
            }

            if (!group.IsEmpty) _subscriptions[package] = group;
        }

        public void Remove(IPersistencePackage package)
        {
            if (!_subscriptions.TryGetValue(package, out var subscription)) return;

            subscription.Dispose();
            _subscriptions.Remove(package);
        }

        public void Clear()
        {
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }

        private sealed class SubscriptionGroup : IDisposable
        {
            private readonly List<IDisposable> _subscriptions = new();

            public bool IsEmpty => _subscriptions.Count == 0;

            public void Add(IDisposable subscription) => _subscriptions.Add(subscription);

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
