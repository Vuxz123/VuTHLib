using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Common
{
    /// <summary>
    /// Defers <c>VManager</c> initialization that would otherwise run in <c>Awake</c>.
    /// <para>
    /// Bootstrap managers are instantiated while the root VContainer scope is being configured, so their
    /// <c>Awake</c> (→ <c>InitializeManager</c>) used to run BEFORE the container was built and their
    /// <c>[Inject] Construct</c> was called. Inside <see cref="BeginDeferral"/> the managers' Awake only queues them;
    /// <see cref="FlushDeferred"/> (registered as the last root build callback, i.e. after every component has been
    /// injected) then initializes them in instantiation order.
    /// </para>
    /// </summary>
    public static class VManagerInitializationGate
    {
        private static readonly List<IDeferredManagerInitialization> Pending = new();
        private static int _depth;

        public static bool IsDeferring => _depth > 0;

        /// <summary>Managers whose Awake runs until the returned scope is disposed are queued instead of initialized.</summary>
        public static IDisposable BeginDeferral()
        {
            _depth++;
            return new DeferralScope();
        }

        /// <summary>Initializes every queued manager (in queue order) and clears the queue.</summary>
        public static void FlushDeferred()
        {
            if (Pending.Count == 0) return;

            var batch = Pending.ToArray();
            Pending.Clear();
            foreach (var manager in batch)
            {
                try
                {
                    manager.RunDeferredInitialization();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        internal static bool TryDefer(IDeferredManagerInitialization manager)
        {
            if (_depth == 0) return false;
            Pending.Add(manager);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Enter Play Mode without domain reload keeps statics alive.
            Pending.Clear();
            _depth = 0;
        }

        private sealed class DeferralScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _depth = Math.Max(0, _depth - 1);
            }
        }
    }

    internal interface IDeferredManagerInitialization
    {
        void RunDeferredInitialization();
    }
}
