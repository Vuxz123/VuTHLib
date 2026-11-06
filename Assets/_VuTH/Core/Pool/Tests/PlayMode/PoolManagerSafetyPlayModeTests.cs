using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Pool.Tests.PlayMode
{
    /// <summary>
    /// Pool state stays consistent: double despawns, stale scheduled despawns, instances destroyed from outside,
    /// transforms under scaled parents, warmup side effects, cleared pools, categories and class pools.
    /// </summary>
    public class PoolManagerSafetyPlayModeTests
    {
        public class AwakeCounter : MonoBehaviour
        {
            public static int Awakes;
            private void Awake() => Awakes++;
        }

        private class Base
        {
        }

        private sealed class Derived : Base
        {
        }

        private readonly List<GameObject> _owned = new();
        private PoolManager _manager;
        private Transform _root;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            foreach (var existing in Object.FindObjectsByType<PoolManager>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            _root = new GameObject("PoolRoot_Test").transform;
            _owned.Add(_root.gameObject);
            _prefab = new GameObject("PooledPrefab_Test");
            _owned.Add(_prefab);

            var managerGo = new GameObject("PoolManager_Test");
            managerGo.SetActive(false);
            _owned.Add(managerGo);
            _manager = managerGo.AddComponent<PoolManager>();
            SetField(_manager, "rootPoolTransform", _root);
            SetField(_manager, "warmupOnAwake", false);
            managerGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            PoolExtensions.SetPoolManager(null);
            if (_manager) Object.DestroyImmediate(_manager.gameObject);
            foreach (var go in _owned)
            {
                if (go) Object.DestroyImmediate(go);
            }

            _owned.Clear();
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private GameObject Spawn() => _manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity).gameObject;

        // ---- Double despawn ----

        [Test]
        public void DespawnTwice_IsRefused_SoTwoSpawnsNeverShareAnInstance()
        {
            var a = Spawn();
            _manager.Despawn(a);
            _manager.Despawn(a);

            var metrics = _manager.GetMetrics();
            Assert.That(metrics.TotalPooledObjects, Is.EqualTo(1), "pooled once");
            Assert.That(metrics.TotalActiveObjects, Is.EqualTo(0));

            var first = Spawn();
            var second = Spawn();
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(_manager.GetMetrics().TotalActiveObjects, Is.EqualTo(2));
        }

        // ---- Stale scheduled despawn ----

        [UnityTest]
        public IEnumerator ScheduledDespawn_OfAnEarlierLife_DoesNotDespawnTheNextUser()
        {
            var a = Spawn();
            _manager.Despawn(a, 0.1f);
            _manager.Despawn(a);
            var reused = Spawn();
            Assert.That(reused, Is.SameAs(a), "the same instance, now used elsewhere");

            yield return new WaitForSeconds(0.25f);

            Assert.That(reused.activeSelf, Is.True);
            Assert.That(_manager.IsSpawned(reused), Is.True);
        }

        // ---- Destroyed from outside ----

        [Test]
        public void InstanceDestroyedWhilePooled_IsSkipped()
        {
            var a = Spawn();
            _manager.Despawn(a);
            Object.DestroyImmediate(a);

            var b = Spawn();

            Assert.That(b != null, Is.True);
            Assert.That(b.activeSelf, Is.True);
        }

        [Test]
        public void InstanceDestroyedWhileSpawned_NoLongerCountsAgainstTheLimit()
        {
            _manager.WarmupPool(new PoolConfig
            {
                prefab = _prefab, preloadCount = 0, maxSize = 1, overflowBehavior = OverflowBehavior.ReturnNull
            });
            Object.DestroyImmediate(Spawn()); // e.g. spawned under a scene that was unloaded

            Assert.That(_manager.Spawn(_prefab.transform, Vector3.zero, Quaternion.identity) != null, Is.True);
            Assert.That(_manager.GetMetrics().TotalActiveObjects, Is.EqualTo(1));
        }

        [Test]
        public void RecycleOldest_WithTheOldestDestroyed_StillSpawns()
        {
            _manager.WarmupPool(new PoolConfig
            {
                prefab = _prefab, preloadCount = 0, maxSize = 2, overflowBehavior = OverflowBehavior.RecycleOldest
            });
            var oldest = Spawn();
            var second = Spawn();
            Object.DestroyImmediate(oldest);

            var third = Spawn();

            Assert.That(third != null, Is.True);
            Assert.That(second.activeSelf, Is.True, "the live instance was not recycled: there was room");
        }

        [Test]
        public void RecycleOldest_RecyclesTheOldestLiveInstance()
        {
            _manager.WarmupPool(new PoolConfig
            {
                prefab = _prefab, preloadCount = 0, maxSize = 2, overflowBehavior = OverflowBehavior.RecycleOldest
            });
            var a = Spawn();
            Spawn();

            var c = Spawn();

            Assert.That(c, Is.SameAs(a));
            Assert.That(_manager.GetMetrics().TotalActiveObjects, Is.EqualTo(2));
        }

        // ---- Transform ----

        [Test]
        public void SpawnUnderAScaledParent_KeepsThePrefabLocalTransform()
        {
            // A Canvas scaled by its CanvasScaler: keeping the world transform used to shrink / move the instance.
            var parent = new GameObject("ScaledParent").transform;
            _owned.Add(parent.gameObject);
            parent.position = new Vector3(10f, 0f, 0f);
            parent.localScale = Vector3.one * 0.5f;
            _prefab.transform.localPosition = new Vector3(1f, 2f, 0f);

            var first = _manager.Spawn(_prefab.transform, parent: parent);
            Assert.That(first.parent, Is.SameAs(parent));
            Assert.That(first.localScale, Is.EqualTo(Vector3.one));
            Assert.That(first.localPosition, Is.EqualTo(new Vector3(1f, 2f, 0f)));

            first.localScale = Vector3.one * 3f;
            _manager.Despawn(first.gameObject);
            var again = _manager.Spawn(_prefab.transform, parent: parent);

            Assert.That(again, Is.SameAs(first));
            Assert.That(again.localScale, Is.EqualTo(Vector3.one), "reset to the prefab's");
        }

        [Test]
        public void SpawnWithDefaultRotation_UsesIdentity()
        {
            var spawned = _manager.Spawn(_prefab.transform, new Vector3(1f, 0f, 0f));

            Assert.That(Quaternion.Angle(spawned.rotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(spawned.position, Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        // ---- Warmup ----

        [Test]
        public void Warmup_DoesNotRunAwake_UntilTheInstanceIsSpawned()
        {
            _prefab.AddComponent<AwakeCounter>();
            AwakeCounter.Awakes = 0;

            _manager.WarmupPool(_prefab, preloadCount: 3);
            Assert.That(AwakeCounter.Awakes, Is.EqualTo(0));

            Spawn();
            Assert.That(AwakeCounter.Awakes, Is.EqualTo(1));
        }

        [Test]
        public void Warmup_OfAnExistingPool_KeepsItsInstances()
        {
            var active = Spawn();

            _manager.WarmupPool(_prefab, preloadCount: 2);
            _manager.Despawn(active);

            Assert.That(_manager.GetMetrics().TotalPooledObjects, Is.EqualTo(3), "the spawned one came back to the same pool");
            Assert.That(_manager.GetMetrics().TotalPools, Is.EqualTo(1));
        }

        // ---- Cleared pools ----

        [UnityTest]
        public IEnumerator ClearPool_InstanceStillSpawned_IsDestroyedWhenDespawned()
        {
            var active = Spawn();

            _manager.ClearPool(_prefab);
            _manager.Despawn(active);
            yield return null;

            Assert.That(active == null, Is.True);
            Assert.That(_manager.GetMetrics().TotalPools, Is.EqualTo(0), "no orphan pool");
        }

        // ---- Categories ----

        [Test]
        public void DespawnCategory_OnlyDespawnsTheInstancesOfThatCategory()
        {
            var enemy = _manager.Spawn(_prefab.transform, PoolSpawnOptions.Default.WithCategory("Enemies")).gameObject;
            var other = Spawn();

            Assert.That(_manager.GetCategoryCount("Enemies"), Is.EqualTo(1));
            _manager.DespawnCategory("Enemies");

            Assert.That(enemy.activeSelf, Is.False);
            Assert.That(other.activeSelf, Is.True, "same prefab, no category");
        }

        // ---- Class pool ----

        [Test]
        public void ClassPool_DespawnTwice_IsRefused_AndBaseTypeDespawnWorks()
        {
            var item = _manager.SpawnClass<Derived>();
            _manager.DespawnClass<Base>(item); // static type: the base class

            _manager.DespawnClass(item);

            var first = _manager.SpawnClass<Derived>();
            var second = _manager.SpawnClass<Derived>();
            Assert.That(first, Is.SameAs(item));
            Assert.That(second, Is.Not.SameAs(item), "pooled once");
        }

        [Test]
        public void ClassPool_KeepsAtMostMaxSizeInstances()
        {
            _manager.ConfigureClassPool<Derived>(new ClassPoolConfig { maxSize = 1 });
            var a = _manager.SpawnClass<Derived>();
            var b = _manager.SpawnClass<Derived>();

            _manager.DespawnClass(a);
            _manager.DespawnClass(b);

            Assert.That(_manager.GetClassStats<Derived>().pooledCount, Is.EqualTo(1));
        }

        // ---- Auto cleanup ----

        [UnityTest]
        public IEnumerator AutoCleanup_ClearsAnUnusedPool()
        {
            _manager.WarmupPool(new PoolConfig
            {
                prefab = _prefab, preloadCount = 2, enableAutoCleanup = true, cleanupInterval = 1f
            });
            _manager.Despawn(Spawn()); // last access now

            yield return new WaitForSeconds(2.5f);

            Assert.That(_manager.GetPrefabStats(_prefab), Is.Null);
        }
    }
}
