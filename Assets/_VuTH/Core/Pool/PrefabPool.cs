using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Runtime state of a single prefab pool: the inactive instances waiting to be reused,
    /// the active instances in spawn order (oldest first), the active count and the per-pool <see cref="PoolStats"/>.
    /// Plain C# data holder used by <see cref="PoolManager"/> and <see cref="PrefabPoolRegistry"/>.
    /// </summary>
    public sealed class PrefabPool
    {
        /// <summary>Source prefab this pool instantiates.</summary>
        public GameObject Prefab;

        /// <summary>Entity id of <see cref="Prefab"/> (the registry key).</summary>
        public int PrefabId;

        /// <summary>Configuration (size limits, overflow behaviour, category…).</summary>
        public PoolConfig Config;

        /// <summary>Inactive instances available for reuse (FIFO).</summary>
        public Queue<GameObject> Pool;

        /// <summary>Spawned instances in spawn order, used by <see cref="OverflowBehavior.RecycleOldest"/>.</summary>
        public Queue<GameObject> SpawnOrder;

        /// <summary>Number of instances currently spawned (active) from this pool.</summary>
        public int ActiveCount;

        /// <summary>Per-pool statistics; may be null for pools not created through <see cref="Create"/>.</summary>
        public PoolStats Stats;

        /// <summary>
        /// Creates an empty pool for <paramref name="prefab"/>. When <paramref name="config"/> is null a default
        /// <see cref="PoolConfig"/> referencing the prefab is used.
        /// </summary>
        public static PrefabPool Create(GameObject prefab, PoolConfig config = null)
        {
            config ??= new PoolConfig { prefab = prefab };

            return new PrefabPool
            {
                Prefab = prefab,
                PrefabId = prefab.GetEntityId(),
                Config = config,
                Pool = new Queue<GameObject>(),
                SpawnOrder = new Queue<GameObject>(),
                Stats = new PoolStats
                {
                    prefabId = prefab.GetEntityId(),
                    poolName = prefab.name,
                    createdTime = Time.time
                }
            };
        }

        /// <summary>
        /// Records a spawn or despawn in <see cref="Stats"/> (creating it lazily) and refreshes the pooled count.
        /// </summary>
        public void RecordStats(bool isSpawn, bool fromPool)
        {
            Stats ??= new PoolStats
            {
                prefabId = PrefabId,
                poolName = Prefab != null ? Prefab.name : "Unknown",
                createdTime = Time.time
            };

            if (isSpawn)
            {
                Stats.RecordSpawn(fromPool);
            }
            else
            {
                Stats.RecordDespawn();
            }

            Stats.UpdatePooledCount(Pool.Count);
        }

        /// <summary>
        /// Removes <paramref name="obj"/> from <see cref="SpawnOrder"/>, preserving the order of the remaining entries.
        /// Does nothing when the spawn order is empty.
        /// </summary>
        public void RemoveFromSpawnOrder(GameObject obj)
        {
            if (SpawnOrder.Count > 0)
            {
                var list = SpawnOrder.ToList();
                list.Remove(obj);
                SpawnOrder = new Queue<GameObject>(list);
            }
        }
    }
}
