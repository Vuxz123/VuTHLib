using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Pool.Tests.EditMode
{
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

        [Test]
        public void Create_WithoutConfig_UsesDefaultConfigAndInitialStats()
        {
            var prefab = NewObject("Bullet");

            var pool = PrefabPool.Create(prefab);

            Assert.That(pool.Prefab, Is.SameAs(prefab));
            Assert.That(pool.Config, Is.Not.Null);
            Assert.That(pool.Config.prefab, Is.SameAs(prefab));
            Assert.That(pool.Pool, Is.Empty);
            Assert.That(pool.SpawnOrder, Is.Empty);
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.Stats, Is.Not.Null);
            Assert.That(pool.Stats.poolName, Is.EqualTo("Bullet"));
            Assert.That(pool.Stats.prefabId, Is.EqualTo(pool.PrefabId));
        }

        [Test]
        public void Create_WithConfig_KeepsGivenConfig()
        {
            var prefab = NewObject("Enemy");
            var config = new PoolConfig { prefab = prefab, maxSize = 3, overflowBehavior = OverflowBehavior.ReturnNull };

            var pool = PrefabPool.Create(prefab, config);

            Assert.That(pool.Config, Is.SameAs(config));
        }

        [Test]
        public void RecordStats_TracksSpawnsDespawnsHitsAndPooledCount()
        {
            var prefab = NewObject("Fx");
            var pool = PrefabPool.Create(prefab);

            pool.RecordStats(isSpawn: true, fromPool: false);
            pool.RecordStats(isSpawn: true, fromPool: false);
            pool.Pool.Enqueue(NewObject("Fx(Clone)"));
            pool.RecordStats(isSpawn: false, fromPool: false);
            pool.RecordStats(isSpawn: true, fromPool: true);

            Assert.That(pool.Stats.totalSpawned, Is.EqualTo(3));
            Assert.That(pool.Stats.totalDespawned, Is.EqualTo(1));
            Assert.That(pool.Stats.poolHits, Is.EqualTo(1));
            Assert.That(pool.Stats.poolMisses, Is.EqualTo(2));
            Assert.That(pool.Stats.activeCount, Is.EqualTo(2));
            Assert.That(pool.Stats.peakActive, Is.EqualTo(2));
            Assert.That(pool.Stats.pooledCount, Is.EqualTo(1));
        }

        [Test]
        public void RecordStats_WhenStatsMissing_CreatesThemLazily()
        {
            var prefab = NewObject("Coin");
            var pool = PrefabPool.Create(prefab);
            pool.Stats = null;

            pool.RecordStats(isSpawn: true, fromPool: false);

            Assert.That(pool.Stats, Is.Not.Null);
            Assert.That(pool.Stats.poolName, Is.EqualTo("Coin"));
            Assert.That(pool.Stats.prefabId, Is.EqualTo(pool.PrefabId));
            Assert.That(pool.Stats.totalSpawned, Is.EqualTo(1));
        }

        [Test]
        public void RemoveFromSpawnOrder_RemovesOnlyThatObject_AndKeepsOrder()
        {
            var pool = PrefabPool.Create(NewObject("P"));
            var a = NewObject("a");
            var b = NewObject("b");
            var c = NewObject("c");
            pool.SpawnOrder.Enqueue(a);
            pool.SpawnOrder.Enqueue(b);
            pool.SpawnOrder.Enqueue(c);

            pool.RemoveFromSpawnOrder(b);

            Assert.That(pool.SpawnOrder, Is.EqualTo(new[] { a, c }));
        }
    }
}
