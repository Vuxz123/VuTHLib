using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Pool.Tests.EditMode
{
    public class ScheduledDespawnQueueTests
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
        public void Process_DespawnsOnlyDueEntries_AndKeepsTheRest()
        {
            var queue = new ScheduledDespawnQueue();
            var early = NewObject("early");
            var exact = NewObject("exact");
            var late = NewObject("late");
            queue.Schedule(early, 1f);
            queue.Schedule(exact, 2f);
            queue.Schedule(late, 5f);

            var despawned = new List<GameObject>();
            queue.Process(2f, despawned.Add);

            Assert.That(despawned, Is.EquivalentTo(new[] { early, exact }));
            Assert.That(queue.Count, Is.EqualTo(1));

            despawned.Clear();
            queue.Process(5f, despawned.Add);

            Assert.That(despawned, Is.EqualTo(new[] { late }));
            Assert.That(queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void Process_DropsDestroyedObjects_WithoutCallingBack()
        {
            var queue = new ScheduledDespawnQueue();
            var doomed = new GameObject("doomed");
            queue.Schedule(doomed, 10f);
            Object.DestroyImmediate(doomed);

            var despawned = new List<GameObject>();
            queue.Process(0f, despawned.Add);

            Assert.That(despawned, Is.Empty);
            Assert.That(queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void Clear_DropsPendingEntries()
        {
            var queue = new ScheduledDespawnQueue();
            queue.Schedule(NewObject("a"), 1f);
            queue.Schedule(NewObject("b"), 1f);

            queue.Clear();

            var despawned = new List<GameObject>();
            queue.Process(100f, despawned.Add);
            Assert.That(queue.Count, Is.EqualTo(0));
            Assert.That(despawned, Is.Empty);
        }
    }
}
