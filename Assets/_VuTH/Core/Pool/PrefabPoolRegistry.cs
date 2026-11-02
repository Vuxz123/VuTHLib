using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Storage and lookup for prefab pools: maps a prefab id to its <see cref="PrefabPool"/> and every
    /// pooled/spawned instance back to the pool that owns it. Holds no Unity lifecycle logic; destruction of
    /// instances is delegated to the caller so the registry can be used (and tested) without a scene.
    /// </summary>
    public sealed class PrefabPoolRegistry
    {
        // Pool Data - Key = prefab InstanceID
        private readonly Dictionary<int, PrefabPool> _pools = new();

        // Track spawned objects to their pool - Key = instance InstanceID
        private readonly Dictionary<int, PrefabPool> _instanceToPool = new();

        /// <summary>Number of registered prefab pools.</summary>
        public int PoolCount => _pools.Count;

        /// <summary>Number of instances currently mapped to a pool.</summary>
        public int TrackedInstanceCount => _instanceToPool.Count;

        /// <summary>All registered pools.</summary>
        public IEnumerable<PrefabPool> Pools => _pools.Values;

        /// <summary>All registered pools keyed by prefab id.</summary>
        public IEnumerable<KeyValuePair<int, PrefabPool>> Entries => _pools;

        /// <summary>Sum of <see cref="PrefabPool.ActiveCount"/> over all pools.</summary>
        public int TotalActiveCount => _pools.Values.Sum(p => p.ActiveCount);

        /// <summary>Sum of inactive (pooled) instances over all pools.</summary>
        public int TotalPooledCount => _pools.Values.Sum(p => p.Pool.Count);

        /// <summary>Looks up the pool registered for <paramref name="prefabId"/>.</summary>
        public bool TryGetPool(int prefabId, out PrefabPool pool) => _pools.TryGetValue(prefabId, out pool);

        /// <summary>Registers (or replaces) the pool for <paramref name="prefabId"/>.</summary>
        public void SetPool(int prefabId, PrefabPool pool) => _pools[prefabId] = pool;

        /// <summary>Unregisters the pool for <paramref name="prefabId"/>. Tracked instances are left untouched.</summary>
        public bool RemovePool(int prefabId) => _pools.Remove(prefabId);

        /// <summary>Maps <paramref name="instance"/> to its owning <paramref name="pool"/> (overwrites any previous mapping).</summary>
        public void TrackInstance(GameObject instance, PrefabPool pool) => _instanceToPool[instance.GetEntityId()] = pool;

        /// <summary>Finds the pool that owns <paramref name="instance"/>.</summary>
        public bool TryGetOwningPool(GameObject instance, out PrefabPool pool) =>
            _instanceToPool.TryGetValue(instance.GetEntityId(), out pool);

        /// <summary>Forgets the owning pool of <paramref name="instance"/>.</summary>
        public bool UntrackInstance(GameObject instance) => _instanceToPool.Remove(instance.GetEntityId());

        /// <summary>
        /// Dequeues inactive instances of <paramref name="pool"/> until at most <paramref name="keepCount"/> remain.
        /// Each dequeued instance that is still alive is untracked and handed to <paramref name="destroy"/>;
        /// destroyed (Unity-null) entries are simply dropped.
        /// </summary>
        public void DrainInactive(PrefabPool pool, int keepCount, Action<GameObject> destroy)
        {
            while (pool.Pool.Count > keepCount)
            {
                var obj = pool.Pool.Dequeue();
                if (obj != null)
                {
                    UntrackInstance(obj);
                    destroy(obj);
                }
            }
        }

        /// <summary>Removes every pool and every instance mapping.</summary>
        public void Clear()
        {
            _pools.Clear();
            _instanceToPool.Clear();
        }

        /// <summary>Estimated memory held by inactive instances (pooled count × <see cref="PoolConfig.estimatedSizeBytes"/>).</summary>
        public long EstimateMemoryUsage()
        {
            long total = 0;
            foreach (var poolData in _pools.Values)
            {
                total += poolData.Pool.Count * poolData.Config.estimatedSizeBytes;
            }
            return total;
        }
    }
}
