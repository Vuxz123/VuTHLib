using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Pool.Tests.PlayMode
{
    public class PoolManagerPlayModeTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<Object> _ownedObjects = new();
        private PoolManager _manager;
        private Transform _root;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            CleanupManagers();

            _root = new GameObject("PoolRoot_Test").transform;
            _ownedObjects.Add(_root.gameObject);

            _prefab = new GameObject("PooledPrefab_Test");
            _ownedObjects.Add(_prefab);

            // Create inactive so serialized fields can be set before Awake (which initializes the manager).
            var managerGo = new GameObject("PoolManager_Test");
            managerGo.SetActive(false);
            _ownedObjects.Add(managerGo);
            _manager = managerGo.AddComponent<PoolManager>();
            SetPrivateField(_manager, "rootPoolTransform", _root);
            SetPrivateField(_manager, "warmupOnAwake", false);
            managerGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            PoolExtensions.SetPoolManager(null);
            CleanupManagers();

            foreach (var ownedObject in _ownedObjects)
            {
                if (ownedObject != null)
                {
                    Object.DestroyImmediate(ownedObject);
                }
            }

            _ownedObjects.Clear();
            _manager = null;
        }

        [Test]
        public void Spawn_Despawn_Respawn_ReusesSameInstance()
        {
            var first = _manager.Spawn(_prefab.transform, Vector3.one, Quaternion.identity);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.gameObject, Is.Not.SameAs(_prefab));
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(first.parent, Is.SameAs(_root));
            Assert.That(first.position, Is.EqualTo(Vector3.one));

            _manager.Despawn(first.gameObject);
            Assert.That(first.gameObject.activeSelf, Is.False);

            var second = _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity);
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void WarmupPool_CreatesInactiveInstances_UnderRoot()
        {
            _manager.WarmupPool(_prefab, preloadCount: 4, maxSize: 10);

            Assert.That(_root.childCount, Is.EqualTo(4));
            for (int i = 0; i < _root.childCount; i++)
            {
                Assert.That(_root.GetChild(i).gameObject.activeSelf, Is.False);
            }

            var metrics = _manager.GetMetrics();
            Assert.That(metrics.TotalPools, Is.EqualTo(1));
            Assert.That(metrics.TotalPooledObjects, Is.EqualTo(4));
            Assert.That(metrics.TotalActiveObjects, Is.EqualTo(0));

            // The first spawn after warmup is served from the pool.
            var spawned = _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity);
            Assert.That(spawned.parent, Is.SameAs(_root));
            Assert.That(_root.childCount, Is.EqualTo(4));
            Assert.That(_manager.GetPrefabStats(_prefab).poolHits, Is.EqualTo(1));
        }

        [Test]
        public void Stats_CountSpawnsDespawnsHitsAndMisses()
        {
            var a = _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity);
            _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity);
            _manager.Despawn(a.gameObject);
            _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity);

            var stats = _manager.GetPrefabStats(_prefab);
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.totalSpawned, Is.EqualTo(3));
            Assert.That(stats.totalDespawned, Is.EqualTo(1));
            Assert.That(stats.poolHits, Is.EqualTo(1));
            Assert.That(stats.poolMisses, Is.EqualTo(2));
            Assert.That(stats.activeCount, Is.EqualTo(2));
            Assert.That(stats.peakActive, Is.EqualTo(2));

            var summary = _manager.GetAnalytics().GetSummary();
            Assert.That(summary.TotalSpawns, Is.EqualTo(3));
            Assert.That(summary.TotalDespawns, Is.EqualTo(1));
            Assert.That(_manager.GetMetrics().TotalActiveObjects, Is.EqualTo(2));
        }

        [Test]
        public void Spawn_WithReturnNullOverflow_ReturnsNull_AndRaisesOverflow()
        {
            _manager.WarmupPool(new PoolConfig
            {
                prefab = _prefab,
                preloadCount = 0,
                maxSize = 1,
                overflowBehavior = OverflowBehavior.ReturnNull
            });
            var overflowCount = 0;
            _manager.OnPoolOverflow += _ => overflowCount++;

            var first = _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity);
            var second = _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity);

            Assert.That(first, Is.Not.Null);
            Assert.That(second == null, Is.True);
            Assert.That(overflowCount, Is.EqualTo(1));
        }

        [Test]
        public void Category_TracksActiveCount_AndDespawnCategoryReturnsObjects()
        {
            var options = PoolSpawnOptions.Default.WithCategory("Enemies");
            var a = _manager.Spawn(_prefab.transform, options);
            var b = _manager.Spawn(_prefab.transform, options);

            Assert.That(_manager.GetCategories(), Is.EquivalentTo(new[] { "Enemies" }));
            Assert.That(_manager.GetCategoryCount("Enemies"), Is.EqualTo(2));

            _manager.DespawnCategory("Enemies");

            Assert.That(a.gameObject.activeSelf, Is.False);
            Assert.That(b.gameObject.activeSelf, Is.False);
            Assert.That(_manager.GetCategoryCount("Enemies"), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Despawn_WithDelay_DespawnsAfterDelayElapses()
        {
            var spawned = _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity);

            _manager.Despawn(spawned.gameObject, 0.1f);
            Assert.That(_manager.GetDebugInfo()["ScheduledDespawns"], Is.EqualTo(1));
            Assert.That(spawned.gameObject.activeSelf, Is.True);

            yield return new WaitForSeconds(0.25f);
            yield return null;

            Assert.That(spawned.gameObject.activeSelf, Is.False);
            Assert.That(_manager.GetDebugInfo()["ScheduledDespawns"], Is.EqualTo(0));
        }

        [Test]
        public void ClearPool_DestroysInactiveInstances_AndForgetsPool()
        {
            _manager.WarmupPool(_prefab, preloadCount: 3, maxSize: 10);
            Assert.That(_manager.GetPrefabStats(_prefab), Is.Not.Null);

            _manager.ClearPool(_prefab);

            Assert.That(_manager.GetPrefabStats(_prefab), Is.Null);
            Assert.That(_manager.GetMetrics().TotalPools, Is.EqualTo(0));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = typeof(PoolManager).GetField(fieldName, InstanceFlags);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on PoolManager");
            field.SetValue(target, value);
        }

        private static void CleanupManagers()
        {
            foreach (var manager in Object.FindObjectsByType<PoolManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (manager)
                {
                    Object.DestroyImmediate(manager.gameObject);
                }
            }
        }
    }
}
