using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// =======================================================================
    /// LAYER 1: CORE OPERATIONS - Basic pool operations only
    /// Responsibility: Spawn/Despawn objects, nothing else
    /// =======================================================================
    /// </summary>
    public interface IPoolOperations
    {
        // GameObject pooling
        T Spawn<T>(T prefab, Vector3 position = default, Quaternion rotation = default, Transform parent = null)
            where T : Component;
        
        void Despawn(GameObject obj, float delay = 0f);
        
        // C# class pooling
        T SpawnClass<T>() where T : class, new();
        void DespawnClass<T>(T obj) where T : class;
    }

    /// <summary>
    /// =======================================================================
    /// LAYER 2: LIFECYCLE MANAGEMENT - Pool lifecycle operations
    /// Responsibility: Initialization, warmup, cleanup
    /// =======================================================================
    /// </summary>
    public interface IPoolLifecycle
    {
        // Warmup
        void WarmupPool(PoolConfig config);
        void WarmupPool(GameObject prefab, int preloadCount = 5, int maxSize = 50);
        void WarmupPools();
        
        // Cleanup
        void ClearPool(GameObject prefab);
        void ClearPool<T>() where T : class;
        void ClearAllPools();
        void TrimExcess(int keepMinimum = 5);
        void CleanupUnused(float unusedTimeSeconds = 60f);
    }

    /// <summary>
    /// =======================================================================
    /// LAYER 3: ORGANIZATION - Grouping and batch operations
    /// Responsibility: Category management, batch operations
    /// =======================================================================
    /// </summary>
    public interface IPoolOrganization
    {
        // Category management
        void DespawnCategory(string category, float delay = 0f);
        void ClearCategory(string category);
        string[] GetCategories();
        int GetCategoryCount(string category);
    }

    /// <summary>
    /// =======================================================================
    /// LAYER 4: ANALYTICS - Statistics and monitoring
    /// Responsibility: Performance metrics, debugging
    /// =======================================================================
    /// </summary>
    public interface IPoolAnalyticsProvider
    {
        // Stats
        PoolStats GetPrefabStats(GameObject prefab);
        PoolStats GetClassStats<T>() where T : class;
        Dictionary<string, PoolStats> GetAllStats();
        
        // Metrics
        PoolMetrics GetMetrics();
        Dictionary<string, object> GetDebugInfo();
        PoolAnalytics GetAnalytics();
        
        // Events
        event Action<GameObject> OnObjectSpawned;
        event Action<GameObject> OnObjectDespawned;
        event Action<int> OnPoolOverflow;
    }

    /// <summary>
    /// =======================================================================
    /// OPTIONS PATTERN - Clean API for spawn operations
    /// =======================================================================
    /// </summary>
    public struct PoolSpawnOptions
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Transform Parent;
        public string Category;
        public float AutoRecycleTime;
        
        public static PoolSpawnOptions Default => new()
        {
            Position = Vector3.zero,
            Rotation = Quaternion.identity,
            AutoRecycleTime = -1f
        };
        
        public static PoolSpawnOptions At(Vector3 position) => new()
        {
            Position = position,
            Rotation = Quaternion.identity,
            AutoRecycleTime = -1f
        };
        
        public static PoolSpawnOptions At(Vector3 position, Quaternion rotation) => new()
        {
            Position = position,
            Rotation = rotation,
            AutoRecycleTime = -1f
        };
        
        public static PoolSpawnOptions WithParent(Transform parent) => new()
        {
            Parent = parent,
            AutoRecycleTime = -1f
        };
        
        public PoolSpawnOptions WithCategory(string category)
        {
            Category = category;
            return this;
        }
        
        public PoolSpawnOptions WithAutoRecycle(float time)
        {
            AutoRecycleTime = time;
            return this;
        }
    }

    /// <summary>
    /// =======================================================================
    /// POOL METRICS - Aggregate statistics for monitoring
    /// =======================================================================
    /// </summary>
    public class PoolMetrics
    {
        public int TotalPools;
        public int TotalActiveObjects;
        public int TotalPooledObjects;
        public float OverallHitRate;
        public long EstimatedMemoryBytes;
        public Dictionary<string, int> CategoryCounts = new();
    }

    /// <summary>
    /// =======================================================================
    /// WHY THIS DESIGN?
    /// =======================================================================
    /// 
    /// ✅ BENEFITS:
    /// 
    /// 1. Single Responsibility Principle
    ///    - Each interface has ONE clear purpose
    ///    - Easy to test each layer independently
    /// 
    /// 2. Interface Segregation Principle
    ///    - Clients only depend on what they need
    ///    - Example: Analytics tool only needs IPoolAnalyticsProvider
    /// 
    /// 3. Dependency Inversion
    ///    - High-level (IPoolManager) depends on abstractions (layers)
    ///    - Easy to swap implementations
    /// 
    /// 4. Open/Closed Principle
    ///    - Extend with new interfaces without modifying existing ones
    ///    - Example: Add IPoolNetworking for multiplayer
    /// 
    /// 5. Clean API Surface
    ///    - Options pattern prevents parameter explosion
    ///    - Fluent, readable code
    /// 
    /// 6. Testability
    ///    - Mock individual layers easily
    ///    - Unit test each concern separately
    /// 
    /// =======================================================================
    /// USAGE EXAMPLES:
    /// =======================================================================
    /// 
    /// // Use case 1: Simple spawning (only needs IPoolOperations)
    /// public class BulletSpawner
    /// {
    ///     private IPoolOperations _pool;
    ///     
    ///     public void Fire()
    ///     {
    ///         var bullet = _pool.Spawn(bulletPrefab, muzzle.position);
    ///     }
    /// }
    /// 
    /// // Use case 2: Analytics dashboard (only needs IPoolAnalyticsProvider)
    /// public class PoolMonitor
    /// {
    ///     private IPoolAnalyticsProvider _analytics;
    ///     
    ///     public void DisplayStats()
    ///     {
    ///         var metrics = _analytics.GetMetrics();
    ///         Debug.Log($"Hit Rate: {metrics.OverallHitRate}");
    ///     }
    /// }
    /// 
    /// // Use case 3: Level manager (needs lifecycle)
    /// public class LevelManager
    /// {
    ///     private IPoolLifecycle _poolLifecycle;
    ///     
    ///     private void OnLevelUnload()
    ///     {
    ///         _poolLifecycle.ClearAllPools();
    ///     }
    /// }
    /// 
    /// // Use case 4: Wave system (needs organization)
    /// public class WaveController
    /// {
    ///     private IPoolOrganization _poolOrg;
    ///     
    ///     public void EndWave()
    ///     {
    ///         _poolOrg.DespawnCategory("CurrentWave");
    ///     }
    /// }
    /// 
    /// // Use case 5: Full control (needs everything)
    /// public class GameManager
    /// {
    ///     private IPoolManager _poolManager; // Has all interfaces
    ///     
    ///     public void ComplexOperation()
    ///     {
    ///         // Can use any layer
    ///         _poolManager.Spawn(prefab, PoolSpawnOptions.At(position));
    ///         _poolManager.GetMetrics();
    ///         _poolManager.ClearCategory("Enemies");
    ///     }
    /// }
    /// 
    /// =======================================================================
    /// </summary>
    internal static class PoolLayersDocumentation { }
}

