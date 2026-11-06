using System;
using UnityEngine;

namespace _VuTH.Core.Pool
{
    /// <summary>What a spawn does when the pool already has <see cref="PoolConfig.maxSize"/> active instances.</summary>
    public enum OverflowBehavior
    {
        [Tooltip("Create another instance (default).")]
        Expand,
        [Tooltip("Return null.")]
        ReturnNull,
        [Tooltip("Despawn the oldest active instance and reuse it.")]
        RecycleOldest
    }

    /// <summary>
    /// Configuration of one prefab pool: warmup, size limits, overflow handling, category and automatic cleanup.
    /// </summary>
    [Serializable]
    public class PoolConfig
    {
        [Header("Prefab")]
        public GameObject prefab;

        [Header("Pool Size")]
        [Tooltip("Inactive instances created by the warmup.")]
        [Min(0)] public int preloadCount = 5;

        [Tooltip("Maximum ACTIVE instances at once; see the overflow behavior. 0 = unlimited.")]
        [Min(0)] public int maxSize = 100;

        [Tooltip("Maximum INACTIVE instances kept in the pool; extra despawned instances are destroyed. 0 = unlimited.")]
        [Min(0)] public int maxPoolSize = 50;

        [Header("Overflow Handling")]
        [Tooltip("What a spawn does when maxSize instances are active.")]
        public OverflowBehavior overflowBehavior = OverflowBehavior.Expand;

        [Header("Category & Cleanup")]
        [Tooltip("Optional category: DespawnCategory despawns every instance of this pool.")]
        public string category;

        [Tooltip("Clear the pool (destroy its inactive instances) once it has had no active instance for cleanupInterval seconds.")]
        public bool enableAutoCleanup;

        [Tooltip("Seconds without any active instance before the automatic cleanup clears the pool.")]
        [Min(1f)] public float cleanupInterval = 60f;

        [Header("Memory")]
        [Tooltip("Estimated size of one instance in bytes, for the memory estimate.")]
        public long estimatedSizeBytes = 1024;
    }

    /// <summary>Configuration of a C# class pool (<see cref="IPoolOperations.SpawnClass{T}"/>).</summary>
    [Serializable]
    public class ClassPoolConfig
    {
        [Tooltip("Maximum instances kept in the pool; extra despawned instances are dropped. 0 = unlimited.")]
        [Min(0)] public int maxSize = 100;
    }
}
