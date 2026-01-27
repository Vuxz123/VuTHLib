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
        
        // Pool Data - Key = InstanceID
        private readonly Dictionary<int, PoolData> _prefabPools = new();
        
        // Track spawned objects to their pool
        private readonly Dictionary<int, PoolData> _instanceToPool = new();
        
        // Class Pools
        private readonly Dictionary<Type, ClassPoolData> _classPools = new();
        
        // Category Management
        private readonly Dictionary<string, HashSet<int>> _categories = new();
        
        // Scheduled Despawns
        private readonly List<ScheduledDespawn> _scheduledDespawns = new();
        
        // Analytics
        private PoolAnalytics _analytics;
        
        #endregion
        
        #region Events (IPoolAnalyticsProvider)
        
        public event Action<GameObject> OnObjectSpawned;
        public event Action<GameObject> OnObjectDespawned;
        public event Action<int> OnPoolOverflow;
        
        #endregion

#if VCONTAINER
        [Inject]
        public void Construct(IObjectResolver container)
        {
            _container = container;
        }

        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            builder.RegisterComponent(this).AsImplementedInterfaces();
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
            var poolData = CreatePoolData(config.prefab, config);
            _prefabPools[prefabId] = poolData;
            
            // Pre-spawn
            for (int i = 0; i < config.preloadCount; i++)
            {
                var obj = InstantiateNew(config.prefab);
                obj.SetActive(false);
                obj.transform.SetParent(rootPoolTransform);
                poolData.Pool.Enqueue(obj);
                _instanceToPool[obj.GetEntityId()] = poolData;
            }
            
            // Add to category if specified
            if (!string.IsNullOrEmpty(config.category))
            {
                AddToCategory(config.category, prefabId);
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
            if (!_prefabPools.TryGetValue(prefabId, out var poolData))
            {
                poolData = CreatePoolData(prefabObj);
                _prefabPools[prefabId] = poolData;
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
            t.SetParent(parent ?? rootPoolTransform);

            spawnedObj.SetActive(true);
            poolData.ActiveCount++;
            poolData.SpawnOrder.Enqueue(spawnedObj);
            
            // Track instance to pool
            _instanceToPool[spawnedObj.GetEntityId()] = poolData;
            
            // Category tracking
            if (!string.IsNullOrEmpty(category))
            {
                AddToCategory(category, prefabId);
            }

            // Lifecycle callbacks
            NotifyPoolables(spawnedObj, true);
            
            // Stats
            UpdateStats(poolData, true, fromPool);
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
            
            var objId = obj.GetEntityId();
            if (!_instanceToPool.TryGetValue(objId, out var poolData))
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
            if (immediate && poolData.SpawnOrder.Count > 0)
            {
                var list = poolData.SpawnOrder.ToList();
                list.Remove(obj);
                poolData.SpawnOrder = new Queue<GameObject>(list);
            }
            
            // Check pool size limit
            if (poolData.Config.maxPoolSize > 0 && poolData.Pool.Count > poolData.Config.maxPoolSize)
            {
                var excess = poolData.Pool.Dequeue();
                _instanceToPool.Remove(excess.GetEntityId());
                Destroy(excess);
                LogX($"Pool size exceeded, destroyed excess object");
            }
            
            // Stats
            UpdateStats(poolData, false, false);
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
        
        private void AddToCategory(string category, int prefabId)
        {
            if (!_categories.TryGetValue(category, out var prefabIds))
            {
                prefabIds = new HashSet<int>();
                _categories[category] = prefabIds;
            }
            prefabIds.Add(prefabId);
        }
        
        public void DespawnCategory(string category, float delay = 0f)
        {
            if (!_categories.TryGetValue(category, out var prefabIds)) return;
            
            foreach (var prefabId in prefabIds)
            {
                if (!_prefabPools.TryGetValue(prefabId, out var poolData)) continue;
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
            return _categories.Keys.ToArray();
        }
        
        public int GetCategoryCount(string category)
        {
            if (!_categories.TryGetValue(category, out var prefabIds)) return 0;
            
            int count = 0;
            foreach (var prefabId in prefabIds)
            {
                if (_prefabPools.TryGetValue(prefabId, out var poolData))
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
            if (!_prefabPools.TryGetValue(prefabId, out var poolData)) return;
            
            while (poolData.Pool.Count > 0)
            {
                var obj = poolData.Pool.Dequeue();
                if (obj == null) continue;
                _instanceToPool.Remove(obj.GetEntityId());
                Destroy(obj);
            }
            
            _prefabPools.Remove(prefabId);
            LogX($"Cleared pool for {prefab.name}");
        }
        
        public void ClearPool<T>() where T : class
        {
            var type = typeof(T);
            _classPools.Remove(type);
        }
        
        public void ClearAllPools()
        {
            foreach (var poolData in _prefabPools.Values)
            {
                while (poolData.Pool.Count > 0)
                {
                    var obj = poolData.Pool.Dequeue();
                    if (obj != null) Destroy(obj);
                }
            }
            
            _prefabPools.Clear();
            _instanceToPool.Clear();
            _classPools.Clear();
            _categories.Clear();
            _scheduledDespawns.Clear();
            _analytics?.Reset();
            
            LogX("Cleared all pools");
        }
        
        public void TrimExcess(int keepMinimum = 5)
        {
            foreach (var poolData in _prefabPools.Values)
            {
                while (poolData.Pool.Count > keepMinimum)
                {
                    var obj = poolData.Pool.Dequeue();
                    if (obj != null)
                    {
                        _instanceToPool.Remove(obj.GetEntityId());
                        Destroy(obj);
                    }
                }
                poolData.Stats?.UpdatePooledCount(poolData.Pool.Count);
            }
            
            LogX($"Trimmed pools to minimum {keepMinimum}");
        }
        
        public void CleanupUnused(float unusedTimeSeconds = 60f)
        {
            var currentTime = Time.time;
            var toRemove = new List<int>();
            
            foreach (var kvp in _prefabPools)
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
                if (_prefabPools.TryGetValue(prefabId, out var poolData))
                {
                    while (poolData.Pool.Count > 0)
                    {
                        var obj = poolData.Pool.Dequeue();
                        if (obj != null)
                        {
                            _instanceToPool.Remove(obj.GetEntityId());
                            Destroy(obj);
                        }
                    }
                    _prefabPools.Remove(prefabId);
                }
            }
            
            LogX($"Cleaned up {toRemove.Count} unused pools");
        }
        
        #endregion

        #region SCHEDULED DESPAWN
        
        private void ScheduleDespawn(GameObject obj, float delay)
        {
            _scheduledDespawns.Add(new ScheduledDespawn
            {
                Object = obj,
                DespawnTime = Time.time + delay
            });
        }
        
        private void Update()
        {
            ProcessScheduledDespawns();
        }
        
        private void ProcessScheduledDespawns()
        {
            for (int i = _scheduledDespawns.Count - 1; i >= 0; i--)
            {
                var scheduled = _scheduledDespawns[i];
                
                if (scheduled.Object == null)
                {
                    _scheduledDespawns.RemoveAt(i);
                    continue;
                }
                
                if (Time.time >= scheduled.DespawnTime)
                {
                    DespawnInternal(scheduled.Object);
                    _scheduledDespawns.RemoveAt(i);
                }
            }
        }
        
        #endregion

        #region STATS & ANALYTICS
        
        public PoolStats GetPrefabStats(GameObject prefab)
        {
            var prefabId = prefab.GetEntityId();
            return _prefabPools.TryGetValue(prefabId, out var poolData) ? poolData.Stats : null;
        }
        
        public PoolStats GetClassStats<T>() where T : class
        {
            return _classPools.TryGetValue(typeof(T), out var poolData) ? poolData.Stats : null;
        }
        
        public Dictionary<string, PoolStats> GetAllStats()
        {
            var result = new Dictionary<string, PoolStats>();
            
            foreach (var kvp in _prefabPools)
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
            foreach (var category in _categories.Keys)
            {
                categoryCounts[category] = GetCategoryCount(category);
            }
            
            return new PoolMetrics
            {
                TotalPools = _prefabPools.Count + _classPools.Count,
                TotalActiveObjects = _prefabPools.Values.Sum(p => p.ActiveCount),
                TotalPooledObjects = _prefabPools.Values.Sum(p => p.Pool.Count),
                OverallHitRate = summary.OverallHitRate,
                EstimatedMemoryBytes = EstimateMemoryUsage(),
                CategoryCounts = categoryCounts
            };
        }
        
        public Dictionary<string, object> GetDebugInfo()
        {
            var summary = _analytics?.GetSummary() ?? default;
            
            return new Dictionary<string, object>
            {
                ["TotalPrefabPools"] = _prefabPools.Count,
                ["TotalClassPools"] = _classPools.Count,
                ["TotalActiveObjects"] = _prefabPools.Values.Sum(p => p.ActiveCount),
                ["TotalPooledObjects"] = _prefabPools.Values.Sum(p => p.Pool.Count),
                ["MemoryEstimate"] = EstimateMemoryUsage(),
                ["HitRate"] = $"{summary.OverallHitRate:P1}",
                ["Categories"] = _categories.Count,
                ["ScheduledDespawns"] = _scheduledDespawns.Count
            };
        }
        
        private void UpdateStats(PoolData poolData, bool isSpawn, bool fromPool)
        {
            poolData.Stats ??= new PoolStats
            {
                prefabId = poolData.PrefabId,
                poolName = poolData.Prefab?.name ?? "Unknown",
                createdTime = Time.time
            };
            
            if (isSpawn)
            {
                poolData.Stats.RecordSpawn(fromPool);
            }
            else
            {
                poolData.Stats.RecordDespawn();
            }
            
            poolData.Stats.UpdatePooledCount(poolData.Pool.Count);
        }
        
        private long EstimateMemoryUsage()
        {
            long total = 0;
            foreach (var poolData in _prefabPools.Values)
            {
                total += poolData.Pool.Count * poolData.Config.estimatedSizeBytes;
            }
            return total;
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
        
        private PoolData CreatePoolData(GameObject prefab, PoolConfig config = null)
        {
            config ??= new PoolConfig { prefab = prefab };
            
            return new PoolData
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
        
        private class PoolData
        {
            public GameObject Prefab;
            public int PrefabId;
            public PoolConfig Config;
            public Queue<GameObject> Pool;
            public Queue<GameObject> SpawnOrder;
            public int ActiveCount;
            public PoolStats Stats;
        }
        
        private class ClassPoolData
        {
            public Type Type;
            public readonly Stack<object> Pool = new();
            public int ActiveCount;
            public PoolStats Stats;
        }
        
        private struct ScheduledDespawn
        {
            public GameObject Object;
            public float DespawnTime;
        }
        
        #endregion
    }
}