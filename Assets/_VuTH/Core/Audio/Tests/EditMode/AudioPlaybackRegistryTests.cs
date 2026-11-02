using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Audio.Tests.EditMode
{
    public class AudioPlaybackRegistryTests
    {
        private readonly List<GameObject> _ownedObjects = new();

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
        }

        [Test]
        public void Register_AssignsSequentialIdsStartingAtOne_AndStoresEntryState()
        {
            var registry = new AudioPlaybackRegistry();
            var sourceA = CreateSource();
            var sourceB = CreateSource();

            var first = registry.Register(sourceA, AudioChannel.Music, 0.5f, isMusic: true);
            var second = registry.Register(sourceB, AudioChannel.Ui, 0.25f, isMusic: false);

            Assert.That(first.PlaybackId, Is.EqualTo(1));
            Assert.That(second.PlaybackId, Is.EqualTo(2));
            Assert.That(first.Source, Is.SameAs(sourceA));
            Assert.That(first.Channel, Is.EqualTo(AudioChannel.Music));
            Assert.That(first.BaseVolume, Is.EqualTo(0.5f));
            Assert.That(first.IsMusic, Is.True);
            Assert.That(first.FadeFactor, Is.EqualTo(1f));
            Assert.That(first.CancellationTokenSource.IsCancellationRequested, Is.False);
            Assert.That(registry.Count, Is.EqualTo(2));
        }

        [Test]
        public void Register_MapsSourceToPlaybackId_AndEntryIsRetrievable()
        {
            var registry = new AudioPlaybackRegistry();
            var source = CreateSource();

            var entry = registry.Register(source, AudioChannel.Sfx, 1f, isMusic: false);

            Assert.That(registry.Contains(entry.PlaybackId), Is.True);
            Assert.That(registry.TryGet(entry.PlaybackId, out var found), Is.True);
            Assert.That(found, Is.SameAs(entry));
            Assert.That(registry.TryGetPlaybackId(source, out var playbackId), Is.True);
            Assert.That(playbackId, Is.EqualTo(entry.PlaybackId));
            Assert.That(registry.Entries, Has.Member(entry));
        }

        [Test]
        public void Remove_RemovesEntryAndSourceMapping()
        {
            var registry = new AudioPlaybackRegistry();
            var source = CreateSource();
            var entry = registry.Register(source, AudioChannel.Sfx, 1f, isMusic: false);

            registry.Remove(entry.PlaybackId, source);

            Assert.That(registry.Contains(entry.PlaybackId), Is.False);
            Assert.That(registry.TryGetPlaybackId(source, out _), Is.False);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void RemoveSource_RemovesOnlyTheSourceMapping()
        {
            var registry = new AudioPlaybackRegistry();
            var source = CreateSource();
            var entry = registry.Register(source, AudioChannel.Sfx, 1f, isMusic: false);

            registry.RemoveSource(source);

            Assert.That(registry.TryGetPlaybackId(source, out _), Is.False);
            Assert.That(registry.Contains(entry.PlaybackId), Is.True);
        }

        [Test]
        public void Clear_RemovesEverything_ButDoesNotReuseIds()
        {
            var registry = new AudioPlaybackRegistry();
            var source = CreateSource();
            registry.Register(source, AudioChannel.Sfx, 1f, isMusic: false);
            registry.Register(source, AudioChannel.Sfx, 1f, isMusic: false);

            registry.Clear();
            var afterClear = registry.Register(source, AudioChannel.Sfx, 1f, isMusic: false);

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(afterClear.PlaybackId, Is.EqualTo(3));
        }

        private AudioSource CreateSource()
        {
            var go = new GameObject("AudioPlaybackRegistryTests_Source");
            _ownedObjects.Add(go);
            return go.AddComponent<AudioSource>();
        }
    }
}
