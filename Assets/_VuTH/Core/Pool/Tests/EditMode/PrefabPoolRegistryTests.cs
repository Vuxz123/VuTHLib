using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Pool.Tests.EditMode
{
    public class PrefabPoolRegistryTests
    {
        private readonly List<GameObject> _owned = new();
        private PrefabPoolRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new PrefabPoolRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _owned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _owned.Clear();
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            _owned.Add(go);
            return go;
        }

        [Test]
        public void AddPool_ThenTryGetPool_ReturnsIt_AndRemovePoolForgetsIt()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));

            _registry.AddPool(pool.PrefabId, pool);

            Assert.That(_registry.TryGetPool(pool.PrefabId, out var found), Is.True);
            Assert.That(found, Is.SameAs(pool));
            Assert.That(_registry.PoolCount, Is.EqualTo(1));

            Assert.That(_registry.RemovePool(pool.PrefabId), Is.True);
            Assert.That(_registry.TryGetPool(pool.PrefabId, out _), Is.False);
        }

        [Test]
        public void AddPool_Twice_Throws_InsteadOfLosingTheFirstPool()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            _registry.AddPool(pool.PrefabId, pool);

            Assert.Throws<System.ArgumentException>(() => _registry.AddPool(pool.PrefabId, PrefabPool.Create(pool.Prefab)));
        }

        [Test]
        public void Track_MapsTheGameObjectToItsState_UntilUntracked()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var go = NewObject("Instance");

            var instance = _registry.Track(go, pool);

            Assert.That(_registry.TryGetInstance(go, out var found), Is.True);
            Assert.That(found, Is.SameAs(instance));
            Assert.That(found.Pool, Is.SameAs(pool));
            Assert.That(_registry.TrackedInstanceCount, Is.EqualTo(1));

            _registry.Untrack(instance);
            Assert.That(_registry.TryGetInstance(go, out _), Is.False);
        }

        [Test]
        public void DestroyedGameObject_IsStillFound_SoItCanBeForgotten()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var go = NewObject("Instance");
            _registry.Track(go, pool);

            Object.DestroyImmediate(go);

            Assert.That(_registry.TryGetInstance(go, out var instance), Is.True);
            Assert.That(instance.IsAlive, Is.False);
        }

        [Test]
        public void DrainInactive_KeepsRequestedCount_AndUntracksAndDestroysTheRest()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            for (var i = 0; i < 3; i++)
            {
                pool.Inactive.Enqueue(_registry.Track(NewObject("i" + i), pool));
            }

            var destroyed = new List<GameObject>();
            _registry.DrainInactive(pool, 1, destroyed.Add);

            Assert.That(destroyed.Count, Is.EqualTo(2));
            Assert.That(pool.InactiveCount, Is.EqualTo(1));
            Assert.That(_registry.TrackedInstanceCount, Is.EqualTo(1));
        }

        [Test]
        public void DrainInactive_UntracksDestroyedEntries_WithoutDestroyingThemAgain()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var dead = _registry.Track(NewObject("dead"), pool);
            var alive = _registry.Track(NewObject("alive"), pool);
            pool.Inactive.Enqueue(dead);
            pool.Inactive.Enqueue(alive);
            Object.DestroyImmediate(dead.GameObject);

            var destroyed = new List<GameObject>();
            _registry.DrainInactive(pool, 0, destroyed.Add);

            Assert.That(destroyed, Is.EqualTo(new[] { alive.GameObject }));
            Assert.That(_registry.TrackedInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void Totals_AndMemoryEstimate_AggregateAllPools()
        {
            var poolA = PrefabPool.Create(NewObject("A"), new PoolConfig { estimatedSizeBytes = 100 });
            var poolB = PrefabPool.Create(NewObject("B"), new PoolConfig { estimatedSizeBytes = 10 });
            _registry.AddPool(1, poolA);
            _registry.AddPool(2, poolB);
            poolA.Inactive.Enqueue(_registry.Track(NewObject("a0"), poolA));
            poolA.AddActive(_registry.Track(NewObject("a1"), poolA));
            poolB.Inactive.Enqueue(_registry.Track(NewObject("b0"), poolB));
            poolB.Inactive.Enqueue(_registry.Track(NewObject("b1"), poolB));

            Assert.That(_registry.TotalActiveCount, Is.EqualTo(1));
            Assert.That(_registry.TotalPooledCount, Is.EqualTo(3));
            Assert.That(_registry.EstimateMemoryUsage(), Is.EqualTo(100 + 2 * 10));
        }

        [Test]
        public void Clear_RemovesPoolsAndInstances()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            _registry.AddPool(pool.PrefabId, pool);
            _registry.Track(NewObject("Instance"), pool);

            _registry.Clear();

            Assert.That(_registry.PoolCount, Is.EqualTo(0));
            Assert.That(_registry.TrackedInstanceCount, Is.EqualTo(0));
        }
    }
}
