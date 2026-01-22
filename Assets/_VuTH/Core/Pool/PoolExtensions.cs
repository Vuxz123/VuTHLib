using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Pool
{
    /// <summary>
    /// Extension methods và helper utilities cho Pool System
    /// </summary>
    public static class PoolExtensions
    {
        // Cache pool manager reference
        private static IPoolManager _poolManager;
        
        public static void SetPoolManager(IPoolManager poolManager)
        {
            _poolManager = poolManager;
        }

        #region GameObject Extensions

        /// <summary>
        /// Tự động despawn GameObject này sau khi inactive
        /// Usage: myObject.AutoDespawnWhenInactive();
        /// </summary>
        public static void AutoDespawnWhenInactive(this GameObject obj, float checkInterval = 0.5f)
        {
            if (_poolManager == null) return;
            
            var behaviour = obj.GetComponent<AutoDespawnBehaviour>() ?? obj.AddComponent<AutoDespawnBehaviour>();
            behaviour.Initialize(_poolManager, checkInterval);
        }

        /// <summary>
        /// Despawn object này về pool
        /// Usage: myObject.ReturnToPool();
        /// </summary>
        public static void ReturnToPool(this GameObject obj, float delay = 0f)
        {
            if (_poolManager != null)
                _poolManager.Despawn(obj, delay);
            else
                UnityEngine.Object.Destroy(obj);
        }

        /// <summary>
        /// Despawn object này về pool sau khi chạy hết ParticleSystem
        /// Usage: particleObject.ReturnToPoolWhenParticleDone();
        /// </summary>
        public static void ReturnToPoolWhenParticleDone(this GameObject obj)
        {
            var ps = obj.GetComponent<ParticleSystem>();
            if (ps != null && _poolManager != null)
            {
                var duration = ps.main.duration + ps.main.startLifetime.constantMax;
                _poolManager.Despawn(obj, duration);
            }
        }

        /// <summary>
        /// Spawn nhiều objects cùng lúc
        /// Usage: bulletPrefab.SpawnMultiple(10, positions);
        /// </summary>
        public static T[] SpawnMultiple<T>(this T prefab, int count, Vector3[] positions = null, 
            Transform parent = null) where T : Component
        {
            if (_poolManager == null) return null;
            
            var results = new T[count];
            for (int i = 0; i < count; i++)
            {
                var pos = positions != null && i < positions.Length ? positions[i] : Vector3.zero;
                results[i] = _poolManager.Spawn(prefab, pos, Quaternion.identity, parent);
            }
            return results;
        }

        /// <summary>
        /// Spawn object tại vị trí của transform này
        /// Usage: spawnPoint.SpawnAt(bulletPrefab);
        /// </summary>
        public static T SpawnAt<T>(this Transform transform, T prefab) where T : Component
        {
            if (_poolManager == null) return null;
            return _poolManager.Spawn(prefab, transform.position, transform.rotation, transform.parent);
        }

        #endregion

        #region Component Extensions

        /// <summary>
        /// Spawn và attach vào component này
        /// Usage: this.SpawnChild(effectPrefab);
        /// </summary>
        public static T SpawnChild<T>(this Component component, T prefab) where T : Component
        {
            if (_poolManager == null) return null;
            return _poolManager.Spawn(prefab, component.transform.position, 
                component.transform.rotation, component.transform);
        }

        /// <summary>
        /// Despawn GameObject của component này
        /// Usage: this.DespawnSelf();
        /// </summary>
        public static void DespawnSelf(this Component component, float delay = 0f)
        {
            component.gameObject.ReturnToPool(delay);
        }

        #endregion

        #region Collection Extensions

        /// <summary>
        /// Despawn tất cả objects trong collection
        /// Usage: enemyList.DespawnAll();
        /// </summary>
        public static void DespawnAll<T>(this IEnumerable<T> collection, float delay = 0f) where T : Component
        {
            if (_poolManager == null) return;
            
            foreach (var item in collection)
            {
                if (item != null)
                    _poolManager.Despawn(item.gameObject, delay);
            }
        }

        /// <summary>
        /// Despawn và clear collection
        /// Usage: projectileList.DespawnAndClear();
        /// </summary>
        public static void DespawnAndClear<T>(this List<T> list, float delay = 0f) where T : Component
        {
            list.DespawnAll(delay);
            list.Clear();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Tạo pool pattern: Spawn -> Use -> Auto return
        /// Usage: PoolExtensions.ExecuteWithPooledObject(bulletPrefab, bullet => bullet.Fire());
        /// </summary>
        public static void ExecuteWithPooledObject<T>(T prefab, Action<T> action, float lifetime = 5f) 
            where T : Component
        {
            if (_poolManager == null) return;
            
            var obj = _poolManager.Spawn(prefab);
            action?.Invoke(obj);
            _poolManager.Despawn(obj.gameObject, lifetime);
        }

        /// <summary>
        /// Pool-friendly delay execution
        /// Usage: this.DelayedPoolAction(2f, () => SpawnEnemy());
        /// </summary>
        public static void DelayedPoolAction(this MonoBehaviour mono, float delay, Action action)
        {
            mono.StartCoroutine(DelayedActionCoroutine(delay, action));
        }

        private static IEnumerator DelayedActionCoroutine(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        #endregion
    }

    #region Helper Components

    /// <summary>
    /// Component tự động despawn object khi inactive
    /// </summary>
    public class AutoDespawnBehaviour : MonoBehaviour
    {
        private IPoolManager _poolManager;
        private float _checkInterval;
        private float _lastCheckTime;

        public void Initialize(IPoolManager poolManager, float checkInterval)
        {
            _poolManager = poolManager;
            _checkInterval = checkInterval;
        }

        private void Update()
        {
            if (Time.time - _lastCheckTime < _checkInterval) return;
            
            _lastCheckTime = Time.time;
            
            if (!gameObject.activeInHierarchy)
            {
                _poolManager?.Despawn(gameObject);
                Destroy(this);
            }
        }
    }

    /// <summary>
    /// Component tự động despawn khi ParticleSystem play xong
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class AutoDespawnParticle : MonoBehaviour, IPoolable
    {
        private ParticleSystem _particleSystem;
        private float _despawnTime;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        public void OnSpawn()
        {
            var main = _particleSystem.main;
            _despawnTime = Time.time + main.duration + main.startLifetime.constantMax;
        }

        public void OnDespawn()
        {
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            if (Time.time >= _despawnTime && !_particleSystem.IsAlive(true))
            {
                gameObject.ReturnToPool();
            }
        }
    }

    /// <summary>
    /// Component tự động despawn sau một khoảng thời gian
    /// </summary>
    public class AutoDespawnTimer : MonoBehaviour, IPoolable
    {
        [SerializeField] private float lifetime = 5f;
        private float _despawnTime;

        public void OnSpawn()
        {
            _despawnTime = Time.time + lifetime;
        }

        public void OnDespawn()
        {
            // Cleanup if needed
        }

        private void Update()
        {
            if (Time.time >= _despawnTime)
            {
                gameObject.ReturnToPool();
            }
        }

        public void SetLifetime(float time)
        {
            lifetime = time;
            _despawnTime = Time.time + lifetime;
        }
    }

    /// <summary>
    /// Component tracking pool stats real-time
    /// </summary>
    public class PoolStatsTracker : MonoBehaviour
    {
        [SerializeField] private GameObject prefabToTrack;
        [SerializeField] private bool showOnGUI = true;
        
        private IPoolManager _poolManager;
        private PoolStats _stats;

        private void Start()
        {
            _poolManager = FindFirstObjectByType<PoolManager>();
        }

        private void Update()
        {
            if (_poolManager != null && prefabToTrack != null)
            {
                _stats = _poolManager.GetPrefabStats(prefabToTrack);
            }
        }

        private void OnGUI()
        {
            if (!showOnGUI || _stats == null) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 210, 150));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"Pool: {prefabToTrack.name}", GUI.skin.GetStyle("boldLabel"));
            GUILayout.Label($"Spawned: {_stats.totalSpawned}");
            GUILayout.Label($"Despawned: {_stats.totalDespawned}");
            GUILayout.Label($"Active: {_stats.activeCount}");
            GUILayout.Label($"Peak: {_stats.peakActive}");
            GUILayout.Label($"Reuse Rate: {_stats.ReuseRate:P0}");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }

    #endregion

    #region Advanced Patterns

    /// <summary>
    /// Pool với lazy initialization pattern
    /// </summary>
    public class LazyPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly IPoolManager _poolManager;
        private readonly int _initialSize;
        private bool _initialized;

        public LazyPool(T prefab, IPoolManager poolManager, int initialSize = 10)
        {
            _prefab = prefab;
            _poolManager = poolManager;
            _initialSize = initialSize;
        }

        private void Initialize()
        {
            if (_initialized) return;
            
            _poolManager.WarmupPool(_prefab.gameObject, _initialSize);
            _initialized = true;
        }

        public T Spawn(Vector3 position = default, Quaternion rotation = default, Transform parent = null)
        {
            if (!_initialized) Initialize();
            return _poolManager.Spawn(_prefab, position, rotation, parent);
        }

        public void Despawn(T obj, float delay = 0f)
        {
            _poolManager.Despawn(obj.gameObject, delay);
        }
    }

    /// <summary>
    /// Object pool pattern với auto-return using IDisposable
    /// Usage: using (var pooled = new PooledObject(prefab, poolManager)) { pooled.Instance.DoSomething(); }
    /// </summary>
    public class PooledObject<T> : IDisposable where T : Component
    {
        private readonly T _instance;
        private readonly IPoolManager _poolManager;
        private bool _disposed;

        public T Instance => _instance;

        public PooledObject(T prefab, IPoolManager poolManager, Vector3 position = default,
            Quaternion rotation = default, Transform parent = null)
        {
            _poolManager = poolManager;
            _instance = poolManager.Spawn(prefab, position, rotation, parent);
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            if (_instance != null)
                _poolManager.Despawn(_instance.gameObject);
            
            _disposed = true;
        }
    }

    /// <summary>
    /// Wave spawner với pool optimization
    /// </summary>
    public class PooledWaveSpawner : MonoBehaviour
    {
        [SerializeField] private Component prefab;
        [SerializeField] private int waveSize = 10;
        [SerializeField] private float spawnInterval = 0.5f;
        [SerializeField] private Transform[] spawnPoints;
        
        private IPoolManager _poolManager;
        private readonly List<GameObject> _activeObjects = new();

        private void Start()
        {
            _poolManager = FindFirstObjectByType<PoolManager>();
            
            // Warmup pool
            _poolManager?.WarmupPool(prefab.gameObject, waveSize, waveSize * 2);
        }

        public void SpawnWave()
        {
            StartCoroutine(SpawnWaveCoroutine());
        }

        private IEnumerator SpawnWaveCoroutine()
        {
            for (int i = 0; i < waveSize; i++)
            {
                var spawnPoint = spawnPoints[i % spawnPoints.Length];
                var obj = _poolManager.Spawn(prefab, spawnPoint.position, spawnPoint.rotation);
                
                _activeObjects.Add(obj.gameObject);
                
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        public void DespawnWave(float delay = 0f)
        {
            foreach (var obj in _activeObjects)
            {
                if (obj != null)
                    _poolManager?.Despawn(obj, delay);
            }
            _activeObjects.Clear();
        }
    }

    #endregion
}

