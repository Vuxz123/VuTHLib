using System;
using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.Log;
using UnityEngine;

#if VCONTAINER
using VContainer;
using VContainer.Unity;
#endif

namespace _VuTH.Core.Pool
{
    /// <summary>
    /// Prefab and C# class pools:
    /// <list type="bullet">
    /// <item>per-instance state: a second despawn is refused, a scheduled despawn never hits a later life of the
    /// instance, and instances destroyed from outside (scene unload) are skipped and forgotten;</item>
    /// <item>limits and overflow handling (Expand / ReturnNull / RecycleOldest);</item>
    /// <item>warmup without running Awake / OnEnable on the warmed instances;</item>
    /// <item>categories, scheduled despawns, automatic cleanup, statistics and analytics.</item>
    /// </list>
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

        [Tooltip("Delayed despawns (Despawn with a delay, auto recycle) count unscaled time: they also run while the game " +
                 "is paused. Off: they wait while Time.timeScale is 0, like the gameplay.")]
        [SerializeField] private bool delaysUseUnscaledTime;

        [Header("Analytics")]
        [Tooltip("Record statistics and analytics on every spawn / despawn. Turn off to save the bookkeeping.")]
        [SerializeField] private bool enableAnalytics = true;

        [Header("Debug")]
        [SerializeField] private bool enableLogging;

        #endregion

        #region Private Fields

        private readonly PrefabPoolRegistry _prefabPools = new();
        private readonly Dictionary<Type, ClassPoolData> _classPools = new();

        // Spawned class instances -> their pool (by reference): despawn by any static type, refuse a second despawn.
        private readonly Dictionary<object, ClassPoolData> _activeClassInstances = new(ReferenceComparer.Instance);

        private readonly PoolCategoryRegistry _categories = new();
        private readonly ScheduledDespawnQueue _scheduledDespawns = new();
        private Action<PooledInstance> _despawnScheduledCallback;
        private PoolAnalytics _analytics = new();

        // Inactive parent of new instances: Awake / OnEnable only run once the instance is really spawned.
        private Transform _staging;
        private float _nextAutoCleanupTime;

        private const float AutoCleanupCheckInterval = 1f;

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

        private float CurrentTime => delaysUseUnscaledTime ? Time.unscaledTime : Time.time;

        #region INITIALIZATION & WARMUP

        protected override void InitializeBootstrap()
        {
            EnsureRoots();

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

        private void EnsureRoots()
        {
            if (rootPoolTransform == null)
            {
                var root = new GameObject("_PoolRoot");
                rootPoolTransform = root.transform;
                DontDestroyOnLoad(root);
            }

            if (_staging == null)
            {
                var staging = new GameObject("_PoolStaging");
                staging.SetActive(false);
                staging.transform.SetParent(transform, false);
                _staging = staging.transform;
            }
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

        /// <summary>
        /// Creates the pool (or applies <paramref name="config"/> to the existing one) and tops it up to
        /// <see cref="PoolConfig.preloadCount"/> inactive instances. An existing pool keeps its instances.
        /// </summary>
        public void WarmupPool(PoolConfig config)
        {
            if (config?.prefab == null)
            {
                this.LogError("WarmupPool: the config has no prefab.");
                return;
            }

            EnsureRoots();
            var pool = GetOrCreatePool(config.prefab, config);
            pool.Config = config;

            if (!string.IsNullOrEmpty(config.category))
            {
                _categories.Add(config.category, pool.PrefabId);
            }

            var created = 0;
            while (pool.InactiveCount < config.preloadCount)
            {
                var instance = CreateInstance(pool);
                instance.GameObject.SetActive(false);
                instance.GameObject.transform.SetParent(rootPoolTransform, false);
                pool.Inactive.Enqueue(instance);
                created++;
            }

            pool.Stats?.UpdatePooledCount(pool.InactiveCount);
            LogX($"Warmed up pool for {config.prefab.name}: {created} new, {pool.InactiveCount} pooled");
        }

        public void WarmupPool(GameObject prefab, int preloadCount = 5, int maxSize = 50)
        {
            if (prefab == null)
            {
                this.LogError("WarmupPool: prefab is null.");
                return;
            }

            // An existing pool keeps its configuration; only its size changes.
            if (_prefabPools.TryGetPool(prefab.GetEntityId(), out var existing))
            {
                existing.Config.preloadCount = preloadCount;
                existing.Config.maxSize = maxSize;
                WarmupPool(existing.Config);
                return;
            }

            WarmupPool(new PoolConfig
            {
                prefab = prefab,
                preloadCount = preloadCount,
                maxSize = maxSize,
                overflowBehavior = OverflowBehavior.Expand
            });
        }

        #endregion

        #region PREFAB / GAMEOBJECT POOLING

        /// <summary>
        /// Spawns an instance of <paramref name="prefab"/>. With a <paramref name="parent"/> and neither position nor
        /// rotation, the instance keeps the prefab's local transform under the parent (UI, child objects); otherwise it
        /// is placed at the world <paramref name="position"/> / <paramref name="rotation"/>.
        /// </summary>
        public T Spawn<T>(T prefab, Vector3 position = default, Quaternion rotation = default,
            Transform parent = null) where T : Component
        {
            // Quaternion == compares by dot product: default == default is false. Equals compares the fields.
            var keepPrefabTransform = parent != null && position == default && rotation.Equals(default(Quaternion));
            return SpawnCore(prefab, position, rotation, parent, keepPrefabTransform, null, -1f);
        }

        /// <summary>
        /// Spawn with PoolSpawnOptions - Clean API pattern
        /// </summary>
        public T Spawn<T>(T prefab, PoolSpawnOptions options) where T : Component
        {
            return SpawnCore(prefab, options.Position, options.Rotation, options.Parent, options.KeepPrefabTransform,
                options.Category, options.AutoRecycleTime);
        }

        public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent,
            string category = null, float autoRecycleTime = -1f) where T : Component
        {
            return SpawnCore(prefab, position, rotation, parent, false, category, autoRecycleTime);
        }

        private T SpawnCore<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent,
            bool keepPrefabTransform, string category, float autoRecycleTime) where T : Component
        {
            if (prefab == null)
            {
                this.LogError($"Spawn<{typeof(T).Name}>: prefab is null.");
                return null;
            }

            EnsureRoots();
            var prefabObj = prefab.gameObject;
            var pool = GetOrCreatePool(prefabObj, null);

            var instance = TakeInactive(pool);
            var fromPool = instance != null;

            if (instance == null)
            {
                if (pool.Config.maxSize > 0 && pool.ActiveCount >= pool.Config.maxSize)
                {
                    // Instances destroyed from outside no longer count against the limit.
                    ForgetDestroyedActive(pool);
                }

                if (pool.Config.maxSize > 0 && pool.ActiveCount >= pool.Config.maxSize)
                {
                    switch (pool.Config.overflowBehavior)
                    {
                        case OverflowBehavior.ReturnNull:
                            LogWarningX($"Pool overflow for {prefabObj.name}. Returning null.");
                            RecordOverflow(pool);
                            return null;

                        case OverflowBehavior.RecycleOldest:
                            var oldest = pool.OldestAlive();
                            if (oldest != null)
                            {
                                DespawnInstance(oldest, allowDestroyExcess: false);
                                instance = TakeInactive(pool);
                                fromPool = instance != null;
                                RecordOverflow(pool);
                            }

                            break;
                    }
                }

                if (instance == null)
                {
                    instance = CreateInstance(pool);
                    if (enableAnalytics) _analytics.RecordPoolMiss(pool.PrefabId);
                }
            }
            else if (enableAnalytics)
            {
                _analytics.RecordPoolHit(pool.PrefabId);
            }

            var go = instance.GameObject;
            Place(go, prefabObj, position, rotation, parent, keepPrefabTransform);

            instance.Generation++;
            instance.Category = string.IsNullOrEmpty(category) ? null : category;
            pool.AddActive(instance);
            if (instance.Category != null) _categories.Add(instance.Category, pool.PrefabId);

            go.SetActive(true);
            NotifyPoolables(instance, true);

            if (enableAnalytics)
            {
                pool.RecordStats(true, fromPool);
                _analytics.RecordSpawn(pool.PrefabId);
            }

            OnObjectSpawned?.Invoke(go);

            if (autoRecycleTime > 0)
            {
                _scheduledDespawns.Schedule(instance, CurrentTime + autoRecycleTime);
            }

            LogX($"Spawned {go.name} (Pool: {pool.InactiveCount}, Active: {pool.ActiveCount})");
            return go.GetComponent<T>();
        }

        /// <summary>Parents the instance and sets its transform; it is still inactive, so nothing runs yet.</summary>
        private void Place(GameObject go, GameObject prefab, Vector3 position, Quaternion rotation, Transform parent,
            bool keepPrefabTransform)
        {
            var t = go.transform;
            t.SetParent(parent != null ? parent : rootPoolTransform, false);

            if (keepPrefabTransform)
            {
                // Same local transform as a fresh instance under this parent, whatever the parent's scale (Canvas).
                var source = prefab.transform;
                if (t is RectTransform rect && source is RectTransform sourceRect)
                {
                    rect.anchorMin = sourceRect.anchorMin;
                    rect.anchorMax = sourceRect.anchorMax;
                    rect.pivot = sourceRect.pivot;
                    rect.sizeDelta = sourceRect.sizeDelta;
                    rect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                }
                else
                {
                    t.localPosition = source.localPosition;
                }

                t.localRotation = source.localRotation;
                t.localScale = source.localScale;
                return;
            }

            // A default Quaternion (0,0,0,0) is not a rotation.
            if (rotation.Equals(default(Quaternion))) rotation = Quaternion.identity;
            t.SetPositionAndRotation(position, rotation);
            t.localScale = prefab.transform.localScale;
        }

        /// <summary>Next reusable instance; instances destroyed while pooled are forgotten.</summary>
        private PooledInstance TakeInactive(PrefabPool pool)
        {
            var dropped = new List<PooledInstance>(0);
            var instance = pool.TakeInactive(dropped);
            foreach (var dead in dropped) _prefabPools.Untrack(dead);
            return instance;
        }

        private void ForgetDestroyedActive(PrefabPool pool)
        {
            foreach (var dead in pool.PruneDestroyedActive())
            {
                _prefabPools.Untrack(dead);
                if (enableAnalytics) pool.Stats?.RecordDespawn();
                LogWarningX($"An instance of {pool.Prefab.name} was destroyed while spawned; it is no longer counted.");
            }
        }

        private void RecordOverflow(PrefabPool pool)
        {
            if (enableAnalytics) _analytics.RecordOverflow(pool.PrefabId);
            OnPoolOverflow?.Invoke(pool.PrefabId);
        }

        public void Despawn(GameObject obj, float delay = 0f)
        {
            if (obj == null) return;

            if (!_prefabPools.TryGetInstance(obj, out var instance))
            {
                LogWarningX($"{obj.name} was not spawned by the pool. Destroying it.");
                Destroy(obj);
                return;
            }

            if (!instance.IsActive)
            {
                // A second despawn would put it in the pool twice, and two spawns would hand it out twice.
                this.LogWarning($"{obj.name} is already despawned; ignored.");
                return;
            }

            if (delay > 0)
            {
                _scheduledDespawns.Schedule(instance, CurrentTime + delay);
                return;
            }

            DespawnInstance(instance, allowDestroyExcess: true);
        }

        [Obsolete("The prefab is not needed: use Despawn(obj).")]
        public void Despawn(GameObject obj, GameObject originalPrefab)
        {
            Despawn(obj);
        }

        public bool IsSpawned(GameObject obj)
        {
            return _prefabPools.TryGetInstance(obj, out var instance) && instance.IsActive;
        }

        private void DespawnInstance(PooledInstance instance, bool allowDestroyExcess)
        {
            if (!instance.IsActive) return;

            var pool = instance.Pool;
            var go = instance.GameObject;
            instance.Generation++; // pending scheduled despawns of this life no longer apply

            if (!instance.IsAlive)
            {
                pool?.RemoveActive(instance);
                _prefabPools.Untrack(instance);
                return;
            }

            NotifyPoolables(instance, false);

            if (pool == null)
            {
                // Its pool was cleared while it was out: it has nowhere to go back to.
                instance.IsActive = false;
                _prefabPools.Untrack(instance);
                Destroy(go);
                OnObjectDespawned?.Invoke(go);
                return;
            }

            pool.RemoveActive(instance);
            go.SetActive(false);
            go.transform.SetParent(rootPoolTransform, false);

            if (allowDestroyExcess && pool.Config.maxPoolSize > 0 && pool.InactiveCount >= pool.Config.maxPoolSize)
            {
                _prefabPools.Untrack(instance);
                Destroy(go);
                LogX($"Pool of {pool.Prefab.name} is full; destroyed the despawned instance.");
            }
            else
            {
                pool.Inactive.Enqueue(instance);
            }

            if (enableAnalytics)
            {
                pool.RecordStats(false, false);
                _analytics.RecordDespawn(pool.PrefabId);
            }

            OnObjectDespawned?.Invoke(go);
            LogX($"Despawned {go.name} (Pool: {pool.InactiveCount}, Active: {pool.ActiveCount})");
        }

        #endregion

        #region C# CLASS POOLING

        public void ConfigureClassPool<T>(ClassPoolConfig config) where T : class
        {
            GetOrCreateClassPool(typeof(T)).Config = config ?? new ClassPoolConfig();
        }

        public T SpawnClass<T>() where T : class, new()
        {
            var type = typeof(T);
            var poolData = GetOrCreateClassPool(type);

            T item;
            var fromPool = false;

            if (poolData.Pool.Count > 0)
            {
                item = (T)poolData.Pool.Pop();
                fromPool = true;
                if (enableAnalytics) _analytics.RecordClassPoolHit(type);
            }
            else
            {
                item = new T();
#if VCONTAINER
                _container?.Inject(item);
#endif
                if (enableAnalytics) _analytics.RecordClassPoolMiss(type);
            }

            poolData.ActiveCount++;
            _activeClassInstances[item] = poolData;

            if (item is IPoolable poolable)
            {
                poolable.OnSpawn();
            }

            if (enableAnalytics)
            {
                poolData.Stats.RecordSpawn(fromPool);
                poolData.Stats.UpdatePooledCount(poolData.Pool.Count);
            }

            LogX($"Spawned class {type.Name} (Pool: {poolData.Pool.Count}, Active: {poolData.ActiveCount})");
            return item;
        }

        /// <summary>Returns <paramref name="obj"/> to its pool, whatever the static type it is passed as.</summary>
        public void DespawnClass<T>(T obj) where T : class
        {
            if (obj == null) return;

            if (!_activeClassInstances.Remove(obj, out var poolData))
            {
                this.LogWarning($"{obj.GetType().Name} instance was not spawned by the pool, or is already despawned; ignored.");
                return;
            }

            if (obj is IPoolable poolable)
            {
                poolable.OnDespawn();
            }

            poolData.ActiveCount--;
            if (!poolData.Cleared && (poolData.Config.maxSize <= 0 || poolData.Pool.Count < poolData.Config.maxSize))
            {
                poolData.Pool.Push(obj);
            }

            if (enableAnalytics)
            {
                poolData.Stats.RecordDespawn();
                poolData.Stats.UpdatePooledCount(poolData.Pool.Count);
            }

            LogX($"Despawned class {poolData.Type.Name} (Pool: {poolData.Pool.Count}, Active: {poolData.ActiveCount})");
        }

        private ClassPoolData GetOrCreateClassPool(Type type)
        {
            if (_classPools.TryGetValue(type, out var poolData)) return poolData;

            poolData = new ClassPoolData { Type = type };
            _classPools[type] = poolData;
            return poolData;
        }

        #endregion

        #region CATEGORY MANAGEMENT

        /// <summary>
        /// Despawns the active instances spawned with <paramref name="category"/>, and every active instance of a pool
        /// configured with that category. Other instances of the same prefab are left alone.
        /// </summary>
        public void DespawnCategory(string category, float delay = 0f)
        {
            foreach (var instance in ActiveInCategory(category))
            {
                if (instance.IsAlive) Despawn(instance.GameObject, delay);
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

        /// <summary>Active instances in <paramref name="category"/> (see <see cref="DespawnCategory"/>).</summary>
        public int GetCategoryCount(string category)
        {
            return ActiveInCategory(category).Count;
        }

        private List<PooledInstance> ActiveInCategory(string category)
        {
            var result = new List<PooledInstance>();
            if (string.IsNullOrEmpty(category) || !_categories.TryGetPrefabIds(category, out var prefabIds)) return result;

            foreach (var prefabId in prefabIds)
            {
                if (!_prefabPools.TryGetPool(prefabId, out var pool)) continue;

                var wholePool = pool.Config.category == category;
                foreach (var instance in pool.Active)
                {
                    if (wholePool || instance.Category == category) result.Add(instance);
                }
            }

            return result;
        }

        #endregion

        #region CLEANUP & MEMORY MANAGEMENT

        /// <summary>
        /// Destroys the inactive instances of <paramref name="prefab"/> and forgets its pool. Instances still spawned
        /// are destroyed when they are despawned.
        /// </summary>
        public void ClearPool(GameObject prefab)
        {
            if (prefab == null) return;

            var prefabId = prefab.GetEntityId();
            if (!_prefabPools.TryGetPool(prefabId, out var pool)) return;

            RemovePool(prefabId, pool);
            LogX($"Cleared pool for {prefab.name}");
        }

        private void RemovePool(int prefabId, PrefabPool pool)
        {
            _prefabPools.DrainInactive(pool, 0, Destroy);
            foreach (var instance in pool.Active)
            {
                instance.Pool = null; // destroyed on despawn
            }

            pool.Active.Clear();
            _prefabPools.RemovePool(prefabId);
        }

        /// <summary>Forgets the class pool of <typeparamref name="T"/>; its spawned instances are dropped on despawn.</summary>
        public void ClearPool<T>() where T : class
        {
            if (!_classPools.Remove(typeof(T), out var poolData)) return;

            poolData.Pool.Clear();
            poolData.Cleared = true; // instances still out are dropped when despawned
        }

        public void ClearAllPools()
        {
            foreach (var pool in _prefabPools.Pools)
            {
                _prefabPools.DrainInactive(pool, 0, Destroy);
                foreach (var instance in pool.Active)
                {
                    instance.Pool = null;
                }
            }

            _prefabPools.Clear();
            _classPools.Clear();
            _activeClassInstances.Clear();
            _categories.Clear();
            _scheduledDespawns.Clear();
            _analytics?.Reset();

            LogX("Cleared all pools");
        }

        public void TrimExcess(int keepMinimum = 5)
        {
            foreach (var pool in _prefabPools.Pools)
            {
                _prefabPools.DrainInactive(pool, keepMinimum, Destroy);
                ForgetDestroyedActive(pool);
                pool.Stats?.UpdatePooledCount(pool.InactiveCount);
            }

            LogX($"Trimmed pools to minimum {keepMinimum}");
        }

        public void CleanupUnused(float unusedTimeSeconds = 60f)
        {
            var removed = RemoveUnusedPools(_ => true, unusedTimeSeconds);
            LogX($"Cleaned up {removed} unused pools");
        }

        /// <summary>Removes the pools matching <paramref name="filter"/> with no active instance for long enough.</summary>
        private int RemoveUnusedPools(Func<PrefabPool, bool> filter, float? unusedTimeSeconds)
        {
            var currentTime = Time.time;
            var toRemove = new List<PrefabPool>();

            foreach (var pool in _prefabPools.Pools)
            {
                if (!filter(pool)) continue;

                ForgetDestroyedActive(pool);
                var threshold = unusedTimeSeconds ?? pool.Config.cleanupInterval;
                if (pool.ActiveCount == 0 && pool.Stats != null && currentTime - pool.Stats.lastAccessTime > threshold)
                {
                    toRemove.Add(pool);
                }
            }

            foreach (var pool in toRemove)
            {
                RemovePool(pool.PrefabId, pool);
            }

            return toRemove.Count;
        }

        #endregion

        #region UPDATE (scheduled despawns, auto cleanup)

        private void Update()
        {
            _despawnScheduledCallback ??= instance => DespawnInstance(instance, allowDestroyExcess: true);
            _scheduledDespawns.Process(CurrentTime, _despawnScheduledCallback);

            if (Time.unscaledTime < _nextAutoCleanupTime) return;
            _nextAutoCleanupTime = Time.unscaledTime + AutoCleanupCheckInterval;
            RemoveUnusedPools(pool => pool.Config.enableAutoCleanup, null);
        }

        #endregion

        #region STATS & ANALYTICS

        public PoolStats GetPrefabStats(GameObject prefab)
        {
            if (prefab == null) return null;
            var prefabId = prefab.GetEntityId();
            return _prefabPools.TryGetPool(prefabId, out var pool) ? pool.Stats : null;
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
                var pool = kvp.Value;
                var name = pool.Prefab ? pool.Prefab.name : $"Unknown_{kvp.Key}";
                if (pool.Stats != null)
                {
                    result[$"[Prefab] {name}"] = pool.Stats;
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

        private PrefabPool GetOrCreatePool(GameObject prefab, PoolConfig config)
        {
            var prefabId = prefab.GetEntityId();
            if (_prefabPools.TryGetPool(prefabId, out var pool)) return pool;

            pool = PrefabPool.Create(prefab, config);
            _prefabPools.AddPool(prefabId, pool);
            return pool;
        }

        /// <summary>
        /// Instantiates under the inactive staging parent, so Awake / OnEnable run only when the instance is spawned,
        /// not on creation, and caches its <see cref="IPoolable"/> components.
        /// </summary>
        private PooledInstance CreateInstance(PrefabPool pool)
        {
            EnsureRoots();
#if VCONTAINER
            var go = _container != null ? _container.Instantiate(pool.Prefab, _staging) : Instantiate(pool.Prefab, _staging);
#else
            var go = Instantiate(pool.Prefab, _staging);
#endif
            go.name = pool.Prefab.name;
            go.SetActive(false); // stays inactive when it leaves the staging parent

            var instance = _prefabPools.Track(go, pool);
            instance.Poolables = go.GetComponentsInChildren<IPoolable>(true);
            return instance;
        }

        private void NotifyPoolables(PooledInstance instance, bool isSpawn)
        {
            foreach (var poolable in instance.Poolables)
            {
                if (poolable is UnityEngine.Object unityObject && !unityObject) continue;

                try
                {
                    if (isSpawn) poolable.OnSpawn();
                    else poolable.OnDespawn();
                }
                catch (Exception e)
                {
                    this.LogError($"{(isSpawn ? "OnSpawn" : "OnDespawn")} of {instance.GameObject.name} threw: {e}");
                }
            }
        }

        private void LogX(string message)
        {
            if (enableLogging)
                this.Log(message);
        }

        private void LogWarningX(string message)
        {
            if (enableLogging)
                this.LogWarning(message);
        }

        #endregion

        #region DATA CLASSES

        private class ClassPoolData
        {
            public Type Type;
            public readonly Stack<object> Pool = new();
            public int ActiveCount;
            public PoolStats Stats = new();
            public ClassPoolConfig Config = new();
            public bool Cleared;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        #endregion
    }
}
