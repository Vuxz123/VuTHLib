using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Storage and lookup for prefab pools: maps a prefab id to its <see cref="PrefabPool"/> and every pooled or spawned
    /// GameObject to its <see cref="PooledInstance"/>. Holds no Unity lifecycle logic; destruction of instances is
    /// delegated to the caller so the registry can be used (and tested) without a scene.
    /// </summary>
    public sealed class PrefabPoolRegistry
    {
        private readonly Dictionary<int, PrefabPool> _pools = new();

        // Keyed by the GameObject itself: a destroyed object keeps its identity, so it can still be found and removed.
        private readonly Dictionary<GameObject, PooledInstance> _instances = new();

        /// <summary>Number of registered prefab pools.</summary>
        public int PoolCount => _pools.Count;

        /// <summary>Number of instances known to the registry.</summary>
        public int TrackedInstanceCount => _instances.Count;

        /// <summary>All registered pools.</summary>
        public IEnumerable<PrefabPool> Pools => _pools.Values;

        /// <summary>All registered pools keyed by prefab id.</summary>
        public IEnumerable<KeyValuePair<int, PrefabPool>> Entries => _pools;

        /// <summary>Sum of <see cref="PrefabPool.ActiveCount"/> over all pools.</summary>
        public int TotalActiveCount
        {
            get
            {
                var total = 0;
                foreach (var pool in _pools.Values) total += pool.ActiveCount;
                return total;
            }
        }

        /// <summary>Sum of inactive (pooled) instances over all pools.</summary>
        public int TotalPooledCount
        {
            get
            {
                var total = 0;
                foreach (var pool in _pools.Values) total += pool.InactiveCount;
                return total;
            }
        }

        /// <summary>Looks up the pool registered for <paramref name="prefabId"/>.</summary>
        public bool TryGetPool(int prefabId, out PrefabPool pool) => _pools.TryGetValue(prefabId, out pool);

        /// <summary>Registers the pool for <paramref name="prefabId"/>. Throws if one is already registered.</summary>
        public void AddPool(int prefabId, PrefabPool pool) => _pools.Add(prefabId, pool);

        /// <summary>Unregisters the pool for <paramref name="prefabId"/>. Its instances are left untouched.</summary>
        public bool RemovePool(int prefabId) => _pools.Remove(prefabId);

        /// <summary>Creates the state of a new instance of <paramref name="pool"/> and tracks it.</summary>
        public PooledInstance Track(GameObject gameObject, PrefabPool pool)
        {
            var instance = new PooledInstance(gameObject, pool);
            _instances[gameObject] = instance;
            return instance;
        }

        /// <summary>Finds the state of <paramref name="gameObject"/>.</summary>
        public bool TryGetInstance(GameObject gameObject, out PooledInstance instance)
        {
            if (gameObject is null)
            {
                instance = null;
                return false;
            }

            return _instances.TryGetValue(gameObject, out instance);
        }

        /// <summary>Forgets <paramref name="instance"/>.</summary>
        public bool Untrack(PooledInstance instance) => _instances.Remove(instance.GameObject);

        /// <summary>
        /// Dequeues inactive instances of <paramref name="pool"/> until at most <paramref name="keepCount"/> remain.
        /// Each one is untracked; those still alive are handed to <paramref name="destroy"/>.
        /// </summary>
        public void DrainInactive(PrefabPool pool, int keepCount, Action<GameObject> destroy)
        {
            while (pool.Inactive.Count > keepCount)
            {
                var instance = pool.Inactive.Dequeue();
                Untrack(instance);
                if (instance.IsAlive) destroy(instance.GameObject);
            }
        }

        /// <summary>Removes every pool and every instance.</summary>
        public void Clear()
        {
            _pools.Clear();
            _instances.Clear();
        }

        /// <summary>Estimated memory held by inactive instances (pooled count × <see cref="PoolConfig.estimatedSizeBytes"/>).</summary>
        public long EstimateMemoryUsage()
        {
            long total = 0;
            foreach (var pool in _pools.Values)
            {
                total += pool.InactiveCount * pool.Config.estimatedSizeBytes;
            }

            return total;
        }
    }
}
