using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Audio.Tests.EditMode
{
    public class AudioSourcePoolTests
    {
        private readonly List<GameObject> _ownedObjects = new();
        private readonly List<int> _createdIndices = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _ownedObjects)
            {
                if (go)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _ownedObjects.Clear();
            _createdIndices.Clear();
        }

        [Test]
        public void Warm_CreatesSourcesUpToTarget_WithOneBasedIndices()
        {
            var pool = new AudioSourcePool(CreateSource);

            pool.Warm(3);
            pool.Warm(3);

            Assert.That(pool.OwnedCount, Is.EqualTo(3));
            Assert.That(pool.AvailableCount, Is.EqualTo(3));
            Assert.That(_createdIndices, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Acquire_PrefersAvailableSourcesBeforeCreating()
        {
            var pool = new AudioSourcePool(CreateSource);
            pool.Warm(1);

            var first = pool.Acquire(maxSources: 4);
            var second = pool.Acquire(maxSources: 4);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(_createdIndices, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(pool.OwnedCount, Is.EqualTo(2));
            Assert.That(pool.AvailableCount, Is.EqualTo(0));
        }

        [Test]
        public void Acquire_WhenMaxReachedAndNoneAvailable_ReturnsNull()
        {
            var pool = new AudioSourcePool(CreateSource);

            var first = pool.Acquire(maxSources: 1);
            var second = pool.Acquire(maxSources: 1);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
            Assert.That(pool.OwnedCount, Is.EqualTo(1));
        }

        [Test]
        public void Return_MakesSourceAvailableOnce()
        {
            var pool = new AudioSourcePool(CreateSource);
            var source = pool.Acquire(maxSources: 1);

            pool.Return(source);
            pool.Return(source);

            Assert.That(pool.AvailableCount, Is.EqualTo(1));
            Assert.That(pool.Acquire(maxSources: 1), Is.SameAs(source));
        }

        [Test]
        public void Clear_ForgetsSources_SoWarmStartsIndexingAgain()
        {
            var pool = new AudioSourcePool(CreateSource);
            pool.Warm(2);

            pool.Clear();
            pool.Warm(1);

            Assert.That(pool.OwnedCount, Is.EqualTo(1));
            Assert.That(pool.AvailableCount, Is.EqualTo(1));
            Assert.That(_createdIndices, Is.EqualTo(new[] { 1, 2, 1 }));
        }

        private AudioSource CreateSource(int index)
        {
            _createdIndices.Add(index);
            var go = new GameObject($"AudioSourcePoolTests_{index}");
            _ownedObjects.Add(go);
            return go.AddComponent<AudioSource>();
        }
    }
}
