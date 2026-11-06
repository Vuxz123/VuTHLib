using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// State of one instance made by a pool. Every spawn and despawn goes through it, so the pool always knows whether
    /// an instance is out (active) or waiting in the pool, can refuse a second despawn, can tell a scheduled despawn
    /// from an earlier life of the instance (<see cref="Generation"/>), and removes it from the spawn order in O(1).
    /// </summary>
    public sealed class PooledInstance
    {
        public PooledInstance(GameObject gameObject, PrefabPool pool)
        {
            GameObject = gameObject;
            Pool = pool;
        }

        public GameObject GameObject { get; }

        /// <summary>The pool owning the instance; null once that pool was cleared (the instance is then destroyed on despawn).</summary>
        public PrefabPool Pool { get; internal set; }

        /// <summary>Spawned and not yet despawned.</summary>
        public bool IsActive { get; internal set; }

        /// <summary>Incremented on every spawn and despawn: a scheduled despawn made in an earlier life no longer applies.</summary>
        public int Generation { get; internal set; }

        /// <summary>Category given at spawn (null when none).</summary>
        public string Category { get; internal set; }

        /// <summary><see cref="IPoolable"/> components of the instance, found once when it is created.</summary>
        public IPoolable[] Poolables { get; internal set; } = System.Array.Empty<IPoolable>();

        /// <summary>Node in <see cref="PrefabPool.Active"/> while active.</summary>
        internal LinkedListNode<PooledInstance> ActiveNode;

        /// <summary>Whether the GameObject still exists (it may be destroyed by a scene unload or by game code).</summary>
        public bool IsAlive => GameObject;
    }

    /// <summary>
    /// Runtime state of a single prefab pool: the inactive instances waiting to be reused, the active instances in spawn
    /// order (oldest first) and the per-pool <see cref="PoolStats"/>. Plain C# data holder used by
    /// <see cref="PoolManager"/> and <see cref="PrefabPoolRegistry"/>.
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
        public readonly Queue<PooledInstance> Inactive = new();

        /// <summary>Spawned instances in spawn order (oldest first), used by <see cref="OverflowBehavior.RecycleOldest"/>.</summary>
        public readonly LinkedList<PooledInstance> Active = new();

        /// <summary>Per-pool statistics.</summary>
        public PoolStats Stats;

        /// <summary>Number of instances currently spawned (active) from this pool.</summary>
        public int ActiveCount => Active.Count;

        /// <summary>Number of instances waiting in the pool.</summary>
        public int InactiveCount => Inactive.Count;

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
                Stats = new PoolStats
                {
                    prefabId = prefab.GetEntityId(),
                    poolName = prefab.name,
                    createdTime = Time.time
                }
            };
        }

        /// <summary>Marks <paramref name="instance"/> active, at the end of the spawn order.</summary>
        public void AddActive(PooledInstance instance)
        {
            instance.IsActive = true;
            instance.ActiveNode = Active.AddLast(instance);
        }

        /// <summary>Removes <paramref name="instance"/> from the active instances in O(1).</summary>
        public void RemoveActive(PooledInstance instance)
        {
            instance.IsActive = false;
            if (instance.ActiveNode != null && instance.ActiveNode.List == Active) Active.Remove(instance.ActiveNode);
            instance.ActiveNode = null;
        }

        /// <summary>Dequeues the next inactive instance that still exists; destroyed ones are dropped and returned in <paramref name="dropped"/>.</summary>
        public PooledInstance TakeInactive(List<PooledInstance> dropped = null)
        {
            while (Inactive.Count > 0)
            {
                var instance = Inactive.Dequeue();
                if (instance.IsAlive) return instance;
                dropped?.Add(instance);
            }

            return null;
        }

        /// <summary>Removes the active instances whose GameObject was destroyed; returns them.</summary>
        public List<PooledInstance> PruneDestroyedActive()
        {
            List<PooledInstance> dead = null;
            for (var node = Active.First; node != null;)
            {
                var next = node.Next;
                if (!node.Value.IsAlive)
                {
                    (dead ??= new List<PooledInstance>()).Add(node.Value);
                    RemoveActive(node.Value);
                }

                node = next;
            }

            return dead ?? new List<PooledInstance>();
        }

        /// <summary>The oldest active instance that still exists, or null.</summary>
        public PooledInstance OldestAlive()
        {
            for (var node = Active.First; node != null; node = node.Next)
            {
                if (node.Value.IsAlive) return node.Value;
            }

            return null;
        }

        /// <summary>Records a spawn or despawn in <see cref="Stats"/> (creating it lazily) and refreshes the pooled count.</summary>
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

            Stats.UpdatePooledCount(Inactive.Count);
        }
    }
}
