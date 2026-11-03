using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Audio.Tests.EditMode
{
    /// <summary>Voice stealing, ducking and volume-curve logic (no AudioSource playback needed).</summary>
    public class AudioFeatureLogicTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go) Object.DestroyImmediate(go);
            }
            _objects.Clear();
        }

        private AudioPlaybackEntry Entry(AudioPlaybackRegistry registry, int priority, float startedAt, bool isMusic = false)
        {
            var go = new GameObject("src");
            _objects.Add(go);
            var entry = registry.Register(go.AddComponent<AudioSource>(), isMusic ? AudioChannel.Music : AudioChannel.Sfx, 1f, isMusic);
            entry.Priority = priority;
            entry.StartedAt = startedAt;
            return entry;
        }

        // ---- voice stealing ----

        [Test]
        public void SelectVictim_PicksLeastImportant_ThenOldest()
        {
            var r = new AudioPlaybackRegistry();
            Entry(r, priority: 100, startedAt: 1f);
            var oldLow = Entry(r, priority: 200, startedAt: 2f);
            Entry(r, priority: 200, startedAt: 3f);

            Assert.AreSame(oldLow, AudioVoiceStealing.SelectVictim(r.Entries, newPriority: 128));
        }

        [Test]
        public void SelectVictim_NeverStealsMusic_OrMoreImportantSounds()
        {
            var r = new AudioPlaybackRegistry();
            Entry(r, priority: 255, startedAt: 0f, isMusic: true);
            Entry(r, priority: 10, startedAt: 1f);

            Assert.IsNull(AudioVoiceStealing.SelectVictim(r.Entries, newPriority: 128),
                "the only one-shot (priority 10) is more important than the new sound (128)");
        }

        [Test]
        public void SelectVictim_EqualPriority_StealsTheOldest()
        {
            var r = new AudioPlaybackRegistry();
            var first = Entry(r, priority: 128, startedAt: 1f);
            Entry(r, priority: 128, startedAt: 5f);

            Assert.AreSame(first, AudioVoiceStealing.SelectVictim(r.Entries, newPriority: 128));
        }

        // ---- ducking ----

        [Test]
        public void Duck_CombinesToLowestFactor_AndRestoresWhenRemoved()
        {
            var d = new AudioDuckController();
            var a = d.Add(AudioChannel.Music, 0.5f, fadeDuration: 0f);
            var b = d.Add(AudioChannel.Music, 0.2f, fadeDuration: 0f);
            d.Tick(0f);
            Assert.AreEqual(0.2f, d.GetFactor(AudioChannel.Music), 1e-4f);
            Assert.AreEqual(1f, d.GetFactor(AudioChannel.Sfx), "other channels are untouched");

            d.Remove(b, fadeDuration: 0f);
            d.Tick(0f);
            Assert.AreEqual(0.5f, d.GetFactor(AudioChannel.Music), 1e-4f);

            d.Remove(a, fadeDuration: 0f);
            d.Tick(0f);
            Assert.AreEqual(1f, d.GetFactor(AudioChannel.Music), 1e-4f);
        }

        [Test]
        public void Duck_FadesOverItsDuration()
        {
            var d = new AudioDuckController();
            d.Add(AudioChannel.Music, 0f, fadeDuration: 1f);

            Assert.IsTrue(d.Tick(0.25f));
            Assert.AreEqual(0.75f, d.GetFactor(AudioChannel.Music), 1e-4f);
            d.Tick(10f);
            Assert.AreEqual(0f, d.GetFactor(AudioChannel.Music), 1e-4f);
            Assert.IsFalse(d.Tick(0.1f), "no change once the target is reached");
        }

        [Test]
        public void DuckHandle_DisposeTwice_ReleasesOnce()
        {
            var releases = 0;
            var handle = new AudioDuckHandle((_, _) => releases++, 1, 0f);

            handle.Dispose();
            handle.Dispose();

            Assert.AreEqual(1, releases);
        }

        // ---- volume curve ----

        [TestCase(AudioVolumeCurve.Linear, 0.5f, 0.5f)]
        [TestCase(AudioVolumeCurve.Perceptual, 0.5f, 0.25f)]
        [TestCase(AudioVolumeCurve.Perceptual, 1f, 1f)]
        [TestCase(AudioVolumeCurve.Perceptual, -1f, 0f)]
        public void ApplyCurve_MapsSliderToGain(AudioVolumeCurve curve, float slider, float expected)
        {
            Assert.AreEqual(expected, AudioVolumeMixer.ApplyCurve(slider, curve), 1e-4f);
        }

        [Test]
        public void Resolve_CurveAppliesToSlidersOnly_AndDuckMultiplies()
        {
            // requested 0.8 (cue volume, not curved) × curve(music 0.5)=0.25 × curve(master 1)=1 × duck 0.5
            var v = AudioVolumeMixer.Resolve(AudioChannel.Music, 0.8f, false, 1f, 0.5f, 1f, 1f,
                AudioVolumeCurve.Perceptual, 0.5f);
            Assert.AreEqual(0.8f * 0.25f * 0.5f, v, 1e-4f);

            Assert.AreEqual(0f, AudioVolumeMixer.Resolve(AudioChannel.Sfx, 1f, true, 1f, 1f, 1f, 1f,
                AudioVolumeCurve.Linear, 1f), "muted");
        }
    }
}
