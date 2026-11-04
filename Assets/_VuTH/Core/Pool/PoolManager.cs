using System;
using System.Collections.Generic;
using System.Linq;
using _VuTH.Common;
using _VuTH.Common.Log;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Advanced Pool Manager with full feature set:
    /// - Auto-tracking & Smart despawn
    /// - Pool limits & overflow handling (Expand/ReturnNull/RecycleOldest)
    /// - Warmup & Preloading
    /// - Analytics & Profiling
    /// - Category grouping
    /// - Auto-cleanup & Memory management
    /// - Scheduled despawn with delay
    /// </summary>
    public class PoolManager : VBootstrapManager<PoolManager, IPoolManager>, IPoolManager
    {
#if VCONTAINER
        private IObjectResolver _container;
#endif

        #region Configuration
        
        [Header("Pool Settings")]
        [SerializeField] private Transform rootPoolTransform;
        [SerializeField] private List<PoolConfig> poolConfigs = new();
        [SerializeField] private bool warmupOnAwake = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableLogging;
        
        #endregion
        
        #region Private Fields
        
        // Prefab pools (prefab id -> pool) & instance -> owning pool lookup
        private readonly PrefabPoolRegistry _prefabPools = new();
        
        // Class Pools
        private readonly Dictionary<Type, ClassPoolData> _classPools = new();
        
        // Category Management
        private readonly PoolCategoryRegistry _categories = new();
        
        // Scheduled Despawns
        private readonly ScheduledDespawnQueue _scheduledDespawns = new();
        private Action<GameObject> _despawnScheduledCallback; // cached to avoid a per-frame allocation

        // Analytics
        private PoolAnalytics _analytics;
        
        #endregion
        
        #region Events (IPoolAnalyticsProvider)
        
        public event Action<GameObject> OnObjectSpawned;
        public event Action<GameObject> OnObjectDespawned;
        public event Action<int> OnPoolOverflow;
        
        #endregion

#if VCONTAINER
        /// <summary>Stores the container used to inject spawned instances.</summary>
        [Inject]
        public void Construct(IObjectResolver container)
        {
            _container = container;
        }
#endif

        #region INITIALIZATION & WARMUP
        
        protected override void InitializeBootstrap()
        {
            _analytics = new PoolAnalytics();
            
            if (rootPoolTransform == null)
            {
                var root = new GameObject("_PoolRoot");
                rootPoolTransform = root.transform;
                DontDestroyOnLoad(root);
            }
            
            // Set pool manager reference for extension methods
            PoolExtensions.SetPoolManager(this);
            
            if (warmupOnAwake)
            {
                WarmupPools();
            }
        }
        
        protected override void DeinitializeBootstrap()
        {
            PoolExtensions.SetPoolManager(null);
            ClearAllPools();
        }
        
        public void WarmupPools()
        {
            if (poolConfigs == null) return;
            
            foreach (var config in poolConfigs)
            {
                if (config.prefab == null) continue;
                WarmupPool(config);
            }
        }
        
        public void WarmupPool(PoolConfig config)
        {
            var prefabId = config.prefab.GetEntityId();
            var poolData = PrefabPool.Create(config.prefab, config);
            _prefabPools.SetPool(prefabId, poolData);
            
            // Pre-spawn
            for (int i = 0; i < config.preloadCount; i++)
            {
                var obj = InstantiateNew(config.prefab);
                obj.SetActive(false);
                obj.transform.SetParent(rootPoolTransform);
                poolData.Pool.Enqueue(obj);
                _prefabPools.TrackInstance(obj, poolData);
            }
            
            // Add to category if specified
            if (!string.IsNullOrEmpty(config.category))
            {
                _categories.Add(config.category, prefabId);
            }
            
            LogX($"Warmed up pool for {config.prefab.name}: {config.preloadCount} objects");
        }
        
        public void WarmupPool(GameObject prefab, int preloadCount = 5, int maxSize = 50)
        {
            var config = new PoolConfig
            {
                prefab = prefab,
                preloadCount = preloadCount,
                maxSize = maxSize,
                overflowBehavior = OverflowBehavior.Expand
            };
            WarmupPool(config);
        }
        
        #endregion

        #region PREFAB / GAMEOBJECT POOLING

        public T Spawn<T>(T prefab, Vector3 position = default, Quaternion rotation = default, 
            Transform parent = null) where T : Component
        {
            return Spawn(prefab, position, rotation, parent, null);
        }
        
        /// <summary>
        /// Spawn with PoolSpawnOptions - Clean API pattern
        /// </summary>
        public T Spawn<T>(T prefab, PoolSpawnOptions options) where T : Component
        {
            return Spawn(prefab, options.Position, options.Rotation, options.Parent, 
                options.Category, options.AutoRecycleTime);
        }
        
        public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent,
            string category = null, float autoRecycleTime = -1f) where T : Component
        {
            var prefabObj = prefab.gameObject;
            var prefabId = prefabObj.GetEntityId();

            // Get or create pool
            if (!_prefabPools.TryGetPool(prefabId, out var poolData))
            {
                poolData = PrefabPool.Create(prefabObj);
                _prefabPools.SetPool(prefabId, poolData);
            }

            GameObject spawnedObj;
            bool fromPool = false;

            // Try get from pool
            if (poolData.Pool.Count > 0)
            {
                spawnedObj = poolData.Pool.Dequeue();
                fromPool = true;
                _analytics.RecordPoolHit(prefabId);
            }
            else
            {
                // Check overflow behavior
                if (poolData.Config.maxSize > 0 && poolData.ActiveCount >= poolData.Config.maxSize)
                {
                    switch (poolData.Config.overflowBehavior)
                    {
                        case OverflowBehavior.ReturnNull:
                            LogWarningX($"Pool overflow for {prefabObj.name}. Returning null.");
                            _analytics.RecordOverflow(prefabId);
                            OnPoolOverflow?.Invoke(prefabId);
                            return null;
                            
                        case OverflowBehavior.RecycleOldest:
                            if (poolData.SpawnOrder.Count > 0)
                            {
                                var oldest = poolData.SpawnOrder.Dequeue();
                                DespawnInternal(oldest, immediate: true);
                                spawnedObj = poolData.Pool.Dequeue();
                                fromPool = true;
                                _analytics.RecordOverflow(prefabId);
                                OnPoolOverflow?.Invoke(prefabId);
                            }
                            else
                            {
                                spawnedObj = InstantiateNew(prefabObj);
                            }
                            break;
                            
                        default: // Expand
                            spawnedObj = InstantiateNew(prefabObj);
                            break;
                    }
                }
                else
                {
                    spawnedObj = InstantiateNew(prefabObj);
                }
                
                _analytics.RecordPoolMiss(prefabId);
            }

            // Setup transform
            var t = spawnedObj.transform;
            t.position = position;
            t.rotation = rotation;
            t.SetParent(parent != null ? parent : rootPoolTransform);

            spawnedObj.SetActive(true);
            poolData.ActiveCount++;
            poolData.SpawnOrder.Enqueue(spawnedObj);
            
            // Track instance to pool
            _prefabPools.TrackInstance(spawnedObj, poolData);
            
            // Category tracking
            if (!string.IsNullOrEmpty(category))
            {
                _categories.Add(category, prefabId);
            }

            // Lifecycle callbacks
            NotifyPoolables(spawnedObj, true);
            
            // Stats
            poolData.RecordStats(true, fromPool);
            _analytics.RecordSpawn(prefabId);
            
            // Event
            OnObjectSpawned?.Invoke(spawnedObj);
            
            // Auto-recycle
            if (autoRecycleTime > 0)
            {
                ScheduleDespawn(spawnedObj, autoRecycleTime);
            }

            LogX($"Spawned {spawnedObj.name} (Pool: {poolData.Pool.Count}, Active: {poolData.ActiveCount})");

            return spawnedObj.GetComponent<T>();
        }

        public void Despawn(GameObject obj, float delay = 0f)
        {
            if (obj == null) return;
            
            if (delay > 0)
            {
                ScheduleDespawn(obj, delay);
            }
            else
            {
                DespawnInternal(obj);
            }
        }
        
        public void Despawn(GameObject obj, GameObject originalPrefab)
        {
            // Legacy support - just call normal Despawn
            Despawn(obj);
        }
        
        private void DespawnInternal(GameObject obj, bool immediate = false)
        {
            if (obj == null) return;
            
            if (!_prefabPools.TryGetOwningPool(obj, out var poolData))
            {
                LogWarningX($"Object {obj.name} not tracked by pool. Destroying instead.");
                Destroy(obj);
                return;
            }

            // Lifecycle callbacks
            NotifyPoolables(obj, false);

            obj.SetActive(false);
            obj.transform.SetParent(rootPoolTransform);

            // Return to pool
            poolData.Pool.Enqueue(obj);
            poolData.ActiveCount--;
            
            // Remove from spawn order if immediate recycle
            if (immediate)
            {
                poolData.RemoveFromSpawnOrder(obj);
            }
            
            // Check pool size limit
            if (poolData.Config.maxPoolSize > 0 && poolData.Pool.Count > poolData.Config.maxPoolSize)
            {
                var excess = poolData.Pool.Dequeue();
                _prefabPools.UntrackInstance(excess);
                Destroy(excess);
                LogX($"Pool size exceeded, destroyed excess object");
            }
            
            // Stats
            poolData.RecordStats(false, false);
            _analytics.RecordDespawn(poolData.PrefabId);
            
            // Event
            OnObjectDespawned?.Invoke(obj);

            LogX($"Despawned {obj.name} (Pool: {poolData.Pool.Count}, Active: {poolData.ActiveCount})");
        }
        
        #endregion

        #region C# CLASS POOLING

        public T SpawnClass<T>() where T : class, new()
        {
            var type = typeof(T);

            if (!_classPools.TryGetValue(type, out var poolData))
            {
                poolData = new ClassPoolData { Type = type };
                _classPools[type] = poolData;
            }

            T item;
            bool fromPool = false;

            if (poolData.Pool.Count > 0)
            {
                item = (T)poolData.Pool.Pop();
                fromPool = true;
                _analytics.RecordClassPoolHit(type);
            }
            else
            {
                item = new T();
#if VCONTAINER
                _container?.Inject(item);
#endif
                _analytics.RecordClassPoolMiss(type);
            }

            poolData.ActiveCount++;

            if (item is IPoolable poolable)
            {
                poolable.OnSpawn();
            }

            // Stats
            poolData.Stats ??= new PoolStats();
            poolData.Stats.RecordSpawn(fromPool);
            poolData.Stats.UpdatePooledCount(poolData.Pool.Count);

            LogX($"Spawned class {type.Name} (Pool: {poolData.Pool.Count}, Active: {poolData.ActiveCount})");

            return item;
        }

        public void DespawnClass<T>(T obj) where T : class
        {
            if (obj == null) return;

            var type = typeof(T);

            if (!_classPools.TryGetValue(type, out var poolData))
            {
                LogWarningX($"Class {type.Name} not tracked by pool");
                return;
            }

            if (obj is IPoolable poolable)
            {
                poolable.OnDespawn();
            }

            poolData.Pool.Push(obj);
            poolData.ActiveCount--;
            
            // Stats
            poolData.Stats?.RecordDespawn();
            poolData.Stats?.UpdatePooledCount(poolData.Pool.Count);

            LogX($"Despawned class {type.Name} (Pool: {poolData.Pool.Count}, Active: {poolData.ActiveCount})");
        }

        #endregion

        #region CATEGORY MANAGEMENT
        
        public void DespawnCategory(string category, float delay = 0f)
        {
            if (!_categories.TryGetPrefabIds(category, out var prefabIds)) return;
            
            foreach (var prefabId in prefabIds)
            {
                if (!_prefabPools.TryGetPool(prefabId, out var poolData)) continue;
                var activeObjects = poolData.SpawnOrder.ToArray();
                foreach (var obj in activeObjects)
                {
                    if (obj != null && obj.activeInHierarchy)
                    {
                        Despawn(obj, delay);
                    }
                }
            }
        }
        
        public void ClearCategory(string category)
        {
            DespawnCategory(category);
            _categories.Remove(category);
        }
        
        public string[] GetCategories()
        {
            return _categories.GetNames();
        }
        
        public int GetCategoryCount(string category)
        {
            if (!_categories.TryGetPrefabIds(category, out var prefabIds)) return 0;
            
            int count = 0;
            foreach (var prefabId in prefabIds)
            {
                if (_prefabPools.TryGetPool(prefabId, out var poolData))
                {
                    count += poolData.ActiveCount;
                }
            }
            return count;
        }
        
        #endregion

        #region CLEANUP & MEMORY MANAGEMENT
        
        public void ClearPool(GameObject prefab)
        {
            var prefabId = prefab.GetEntityId();
            if (!_prefabPools.TryGetPool(prefabId, out var poolData)) return;
            
            _prefabPools.DrainInactive(poolData, 0, Destroy);
            
            _prefabPools.RemovePool(prefabId);
            LogX($"Cleared pool for {prefab.name}");
        }
        
        public void ClearPool<T>() where T : class
        {
            var type = typeof(T);
            _classPools.Remove(type);
        }
        
        public void ClearAllPools()
        {
            foreach (var poolData in _prefabPools.Pools)
            {
                _prefabPools.DrainInactive(poolData, 0, Destroy);
            }
            
            _prefabPools.Clear();
            _classPools.Clear();
            _categories.Clear();
            _scheduledDespawns.Clear();
            _analytics?.Reset();
            
            LogX("Cleared all pools");
        }
        
        public void TrimExcess(int keepMinimum = 5)
        {
            foreach (var poolData in _prefabPools.Pools)
            {
                _prefabPools.DrainInactive(poolData, keepMinimum, Destroy);
                poolData.Stats?.UpdatePooledCount(poolData.Pool.Count);
            }
            
            LogX($"Trimmed pools to minimum {keepMinimum}");
        }
        
        public void CleanupUnused(float unusedTimeSeconds = 60f)
        {
            var currentTime = Time.time;
            var toRemove = new List<int>();
            
            foreach (var kvp in _prefabPools.Entries)
            {
                var poolData = kvp.Value;
                if (poolData.Stats != null &&
                    currentTime - poolData.Stats.lastAccessTime > unusedTimeSeconds &&
                    poolData.ActiveCount == 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (var prefabId in toRemove)
            {
                if (_prefabPools.TryGetPool(prefabId, out var poolData))
                {
                    _prefabPools.DrainInactive(poolData, 0, Destroy);
                    _prefabPools.RemovePool(prefabId);
                }
            }
            
            LogX($"Cleaned up {toRemove.Count} unused pools");
        }
        
        #endregion

        #region SCHEDULED DESPAWN
        
        private void ScheduleDespawn(GameObject obj, float delay)
        {
            _scheduledDespawns.Schedule(obj, Time.time + delay);
        }
        
        private void Update()
        {
            ProcessScheduledDespawns();
        }
        
        private void ProcessScheduledDespawns()
        {
            _despawnScheduledCallback ??= obj => DespawnInternal(obj);
            _scheduledDespawns.Process(Time.time, _despawnScheduledCallback);
        }
        
        #endregion

        #region STATS & ANALYTICS
        
        public PoolStats GetPrefabStats(GameObject prefab)
        {
            var prefabId = prefab.GetEntityId();
            return _prefabPools.TryGetPool(prefabId, out var poolData) ? poolData.Stats : null;
        }
        
        public PoolStats GetClassStats<T>() where T : class
        {
            return _classPools.TryGetValue(typeof(T), out var poolData) ? poolData.Stats : null;
        }
        
        public Dictionary<string, PoolStats> GetAllStats()
        {
            var result = new Dictionary<string, PoolStats>();
            
            foreach (var kvp in _prefabPools.Entries)
            {
                var poolData = kvp.Value;
                var name = poolData.Prefab ? poolData.Prefab.name : $"Unknown_{kvp.Key}";
                if (poolData.Stats != null)
                {
                    result[$"[Prefab] {name}"] = poolData.Stats;
                }
            }
            
            foreach (var kvp in _classPools)
            {
                if (kvp.Value.Stats != null)
                {
                    result[$"[Class] {kvp.Key.Name}"] = kvp.Value.Stats;
                }
            }
            
            return result;
        }
        
        public PoolAnalytics GetAnalytics() => _analytics;
        
        public PoolMetrics GetMetrics()
        {
            var summary = _analytics?.GetSummary() ?? default;
            
            var categoryCounts = new Dictionary<string, int>();
            foreach (var category in _categories.Names)
            {
                categoryCounts[category] = GetCategoryCount(category);
            }
            
            return new PoolMetrics
            {
                TotalPools = _prefabPools.PoolCount + _classPools.Count,
                TotalActiveObjects = _prefabPools.TotalActiveCount,
                TotalPooledObjects = _prefabPools.TotalPooledCount,
                OverallHitRate = summary.OverallHitRate,
                EstimatedMemoryBytes = _prefabPools.EstimateMemoryUsage(),
                CategoryCounts = categoryCounts
            };
        }
        
        public Dictionary<string, object> GetDebugInfo()
        {
            var summary = _analytics?.GetSummary() ?? default;
            
            return new Dictionary<string, object>
            {
                ["TotalPrefabPools"] = _prefabPools.PoolCount,
                ["TotalClassPools"] = _classPools.Count,
                ["TotalActiveObjects"] = _prefabPools.TotalActiveCount,
                ["TotalPooledObjects"] = _prefabPools.TotalPooledCount,
                ["MemoryEstimate"] = _prefabPools.EstimateMemoryUsage(),
                ["HitRate"] = $"{summary.OverallHitRate:P1}",
                ["Categories"] = _categories.Count,
                ["ScheduledDespawns"] = _scheduledDespawns.Count
            };
        }
        
        #endregion

        #region HELPER METHODS
        
        private GameObject InstantiateNew(GameObject prefab)
        {
#if VCONTAINER
            return _container != null ? _container.Instantiate(prefab) : Instantiate(prefab);
#else
            return Instantiate(prefab);
#endif
        }
        
        private void NotifyPoolables(GameObject obj, bool isSpawn)
        {
            var poolables = obj.GetComponentsInChildren<IPoolable>(true);
            foreach (var poolable in poolables)
            {
                if (isSpawn) poolable.OnSpawn();
                else poolable.OnDespawn();
            }
        }
        
        private void LogX(string message)
        {
            if (enableLogging)
                this.Log($"{message}");
        }
        
        private void LogWarningX(string message)
        {
            if (enableLogging)
                this.LogWarning($"[PoolManager] {message}");
        }
        
        #endregion

        #region DATA CLASSES
        
        private class ClassPoolData
        {
            public Type Type;
            public readonly Stack<object> Pool = new();
            public int ActiveCount;
            public PoolStats Stats;
        }
        
        #endregion
    }
}