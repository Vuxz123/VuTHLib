using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Pool.Tests.EditMode
{
    /// <summary>Per-pool state: active instances in spawn order, inactive queue, destroyed instances, stats.</summary>
    public class PrefabPoolTests
    {
        private readonly List<GameObject> _owned = new();

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

        private PooledInstance NewInstance(PrefabPool pool, string name) => new(NewObject(name), pool);

        [Test]
        public void Create_WithoutConfig_UsesDefaultConfigAndInitialStats()
        {
            var prefab = NewObject("Prefab");

            var pool = PrefabPool.Create(prefab);

            Assert.That(pool.Prefab, Is.SameAs(prefab));
            Assert.That(pool.Config.prefab, Is.SameAs(prefab));
            Assert.That(pool.InactiveCount, Is.EqualTo(0));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.Stats.poolName, Is.EqualTo("Prefab"));
        }

        [Test]
        public void Create_WithConfig_KeepsGivenConfig()
        {
            var config = new PoolConfig { maxSize = 3 };

            var pool = PrefabPool.Create(NewObject("Prefab"), config);

            Assert.That(pool.Config, Is.SameAs(config));
        }

        [Test]
        public void AddAndRemoveActive_KeepSpawnOrder_InConstantTime()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var a = NewInstance(pool, "a");
            var b = NewInstance(pool, "b");
            var c = NewInstance(pool, "c");
            pool.AddActive(a);
            pool.AddActive(b);
            pool.AddActive(c);

            pool.RemoveActive(b);

            Assert.That(pool.Active, Is.EqualTo(new[] { a, c }));
            Assert.That(b.IsActive, Is.False);
            Assert.That(a.IsActive, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(2));
        }

        [Test]
        public void RemoveActive_Twice_IsHarmless()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var a = NewInstance(pool, "a");
            pool.AddActive(a);

            pool.RemoveActive(a);
            pool.RemoveActive(a);

            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void TakeInactive_SkipsDestroyedInstances()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var dead = NewInstance(pool, "dead");
            var alive = NewInstance(pool, "alive");
            pool.Inactive.Enqueue(dead);
            pool.Inactive.Enqueue(alive);
            Object.DestroyImmediate(dead.GameObject);
            var dropped = new List<PooledInstance>();

            Assert.That(pool.TakeInactive(dropped), Is.SameAs(alive));
            Assert.That(dropped, Is.EqualTo(new[] { dead }));
            Assert.That(pool.TakeInactive(), Is.Null);
        }

        [Test]
        public void PruneDestroyedActive_AndOldestAlive_IgnoreDestroyedInstances()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            var dead = NewInstance(pool, "dead");
            var alive = NewInstance(pool, "alive");
            pool.AddActive(dead);
            pool.AddActive(alive);
            Object.DestroyImmediate(dead.GameObject);

            Assert.That(pool.OldestAlive(), Is.SameAs(alive));
            Assert.That(pool.PruneDestroyedActive(), Is.EqualTo(new[] { dead }));
            Assert.That(pool.Active, Is.EqualTo(new[] { alive }));
        }

        [Test]
        public void RecordStats_TracksSpawnsDespawnsHitsAndPooledCount()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));

            pool.RecordStats(isSpawn: true, fromPool: false);
            pool.RecordStats(isSpawn: true, fromPool: true);
            pool.Inactive.Enqueue(NewInstance(pool, "x"));
            pool.RecordStats(isSpawn: false, fromPool: false);

            Assert.That(pool.Stats.totalSpawned, Is.EqualTo(2));
            Assert.That(pool.Stats.totalDespawned, Is.EqualTo(1));
            Assert.That(pool.Stats.poolHits, Is.EqualTo(1));
            Assert.That(pool.Stats.poolMisses, Is.EqualTo(1));
            Assert.That(pool.Stats.activeCount, Is.EqualTo(1));
            Assert.That(pool.Stats.pooledCount, Is.EqualTo(1));
        }

        [Test]
        public void RecordStats_WhenStatsMissing_CreatesThemLazily()
        {
            var pool = PrefabPool.Create(NewObject("Prefab"));
            pool.Stats = null;

            pool.RecordStats(isSpawn: true, fromPool: false);

            Assert.That(pool.Stats, Is.Not.Null);
            Assert.That(pool.Stats.totalSpawned, Is.EqualTo(1));
        }
    }
}
