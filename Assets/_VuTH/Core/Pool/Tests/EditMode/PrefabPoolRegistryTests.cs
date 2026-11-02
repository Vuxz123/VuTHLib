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
        public void SetPool_ThenTryGetPool_ReturnsIt_AndRemovePoolForgetsIt()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));

            _registry.SetPool(42, pool);

            Assert.That(_registry.PoolCount, Is.EqualTo(1));
            Assert.That(_registry.TryGetPool(42, out var found), Is.True);
            Assert.That(found, Is.SameAs(pool));
            Assert.That(_registry.TryGetPool(7, out _), Is.False);

            Assert.That(_registry.RemovePool(42), Is.True);
            Assert.That(_registry.TryGetPool(42, out _), Is.False);
            Assert.That(_registry.PoolCount, Is.EqualTo(0));
        }

        [Test]
        public void TrackInstance_MapsInstanceToOwningPool_UntilUntracked()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var instance = NewObject("Instance");
            var stranger = NewObject("Stranger");

            _registry.TrackInstance(instance, pool);

            Assert.That(_registry.TryGetOwningPool(instance, out var owner), Is.True);
            Assert.That(owner, Is.SameAs(pool));
            Assert.That(_registry.TryGetOwningPool(stranger, out _), Is.False);
            Assert.That(_registry.TrackedInstanceCount, Is.EqualTo(1));

            Assert.That(_registry.UntrackInstance(instance), Is.True);
            Assert.That(_registry.TryGetOwningPool(instance, out _), Is.False);
        }

        [Test]
        public void DrainInactive_KeepsRequestedCount_AndUntracksAndDestroysTheRest()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var instances = new[] { NewObject("i0"), NewObject("i1"), NewObject("i2"), NewObject("i3") };
            foreach (var instance in instances)
            {
                pool.Pool.Enqueue(instance);
                _registry.TrackInstance(instance, pool);
            }

            var destroyed = new List<GameObject>();
            _registry.DrainInactive(pool, 1, destroyed.Add);

            Assert.That(destroyed, Is.EqualTo(new[] { instances[0], instances[1], instances[2] }));
            Assert.That(pool.Pool, Is.EqualTo(new[] { instances[3] }));
            Assert.That(_registry.TryGetOwningPool(instances[0], out _), Is.False);
            Assert.That(_registry.TryGetOwningPool(instances[3], out _), Is.True);
        }

        [Test]
        public void DrainInactive_SkipsDestroyedEntries()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var dead = new GameObject("dead");
            var alive = NewObject("alive");
            pool.Pool.Enqueue(dead);
            pool.Pool.Enqueue(alive);
            Object.DestroyImmediate(dead);

            var destroyed = new List<GameObject>();
            _registry.DrainInactive(pool, 0, destroyed.Add);

            Assert.That(destroyed, Is.EqualTo(new[] { alive }));
            Assert.That(pool.Pool, Is.Empty);
        }

        [Test]
        public void Totals_AndMemoryEstimate_AggregateAllPools()
        {
            var poolA = PrefabPool.Create(NewObject("A"), new PoolConfig { estimatedSizeBytes = 100 });
            var poolB = PrefabPool.Create(NewObject("B"), new PoolConfig { estimatedSizeBytes = 10 });
            poolA.ActiveCount = 2;
            poolA.Pool.Enqueue(NewObject("a0"));
            poolB.ActiveCount = 1;
            poolB.Pool.Enqueue(NewObject("b0"));
            poolB.Pool.Enqueue(NewObject("b1"));
            _registry.SetPool(1, poolA);
            _registry.SetPool(2, poolB);

            Assert.That(_registry.TotalActiveCount, Is.EqualTo(3));
            Assert.That(_registry.TotalPooledCount, Is.EqualTo(3));
            Assert.That(_registry.EstimateMemoryUsage(), Is.EqualTo(100L + 2 * 10L));
        }

        [Test]
        public void Clear_RemovesPoolsAndInstanceMappings()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var instance = NewObject("Instance");
            _registry.SetPool(1, pool);
            _registry.TrackInstance(instance, pool);

            _registry.Clear();

            Assert.That(_registry.PoolCount, Is.EqualTo(0));
            Assert.That(_registry.TrackedInstanceCount, Is.EqualTo(0));
            Assert.That(_registry.TryGetOwningPool(instance, out _), Is.False);
        }
    }
}
