using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Pool.Tests.EditMode
{
    /// <summary>Scheduled despawns apply only to the life of the instance they were made for.</summary>
    public class ScheduledDespawnQueueTests
    {
        private readonly List<GameObject> _owned = new();
        private ScheduledDespawnQueue _queue;
        private PrefabPool _pool;

        [SetUp]
        public void SetUp()
        {
            _queue = new ScheduledDespawnQueue();
            _pool = PrefabPool.Create(NewObject("Prefab"));
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

        /// <summary>A spawned instance (active, first life).</summary>
        private PooledInstance Spawned(string name)
        {
            var instance = new PooledInstance(NewObject(name), _pool) { Generation = 1 };
            _pool.AddActive(instance);
            return instance;
        }

        [Test]
        public void Process_DespawnsOnlyDueEntries_AndKeepsTheRest()
        {
            var early = Spawned("early");
            var late = Spawned("late");
            _queue.Schedule(early, 1f);
            _queue.Schedule(late, 5f);
            var despawned = new List<PooledInstance>();

            _queue.Process(2f, despawned.Add);

            Assert.That(despawned, Is.EqualTo(new[] { early }));
            Assert.That(_queue.Count, Is.EqualTo(1));
        }

        [Test]
        public void Process_DropsDestroyedObjects_WithoutCallingBack()
        {
            var instance = Spawned("dead");
            _queue.Schedule(instance, 0f);
            Object.DestroyImmediate(instance.GameObject);
            var despawned = new List<PooledInstance>();

            _queue.Process(1f, despawned.Add);

            Assert.That(despawned, Is.Empty);
            Assert.That(_queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void Process_DropsAScheduleOfAnEarlierLife()
        {
            // Despawn(obj, 2f), then a manual despawn and a respawn elsewhere: the old schedule must not fire.
            var instance = Spawned("reused");
            _queue.Schedule(instance, 2f);
            instance.Generation += 2; // despawned, then spawned again
            var despawned = new List<PooledInstance>();

            _queue.Process(3f, despawned.Add);

            Assert.That(despawned, Is.Empty);
            Assert.That(_queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void Process_DropsTheScheduleOfAnInstanceAlreadyDespawned()
        {
            var instance = Spawned("gone");
            _queue.Schedule(instance, 0f);
            _pool.RemoveActive(instance);
            var despawned = new List<PooledInstance>();

            _queue.Process(1f, despawned.Add);

            Assert.That(despawned, Is.Empty);
        }

        [Test]
        public void Clear_DropsPendingEntries()
        {
            _queue.Schedule(Spawned("a"), 1f);

            _queue.Clear();

            Assert.That(_queue.Count, Is.EqualTo(0));
        }
    }
}
