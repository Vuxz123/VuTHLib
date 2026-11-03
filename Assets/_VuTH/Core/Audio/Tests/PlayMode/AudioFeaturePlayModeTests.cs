using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Audio.Tests.PlayMode
{
    /// <summary>Voice stealing, 3D playback, channel pause, ducking and per-channel stop on real AudioSources.</summary>
    public class AudioFeaturePlayModeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private readonly List<Object> _owned = new();
        private AudioManager _manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var m in Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None))
            {
                if (m) Object.DestroyImmediate(m.gameObject);
            }

            var listener = new GameObject("Listener_Test");
            listener.AddComponent<AudioListener>();
            _owned.Add(listener);

            var go = new GameObject("AudioManager_Test");
            _manager = go.AddComponent<AudioManager>();
            _owned.Add(go);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var o in _owned)
            {
                if (o) Object.Destroy(o);
            }
            _owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullPool_StealsLeastImportantSound_ButNotMoreImportantOnes()
        {
            Set(_manager, "maxOneShotSources", 2);
            Set(_manager, "_oneShotPool", null); // fresh pool limited to 2

            var low = _manager.PlaySfx(Cue("low", priority: 200, loop: true));
            var mid = _manager.PlaySfx(Cue("mid", priority: 100, loop: true));
            yield return null;

            var important = _manager.PlaySfx(Cue("important", priority: 10, loop: true));
            Assert.IsTrue(important.IsValid, "an important sound takes the slot of the least important one");
            Assert.IsFalse(low.IsValid);
            Assert.IsTrue(mid.IsValid);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("No available one-shot source"));
            var trivial = _manager.PlaySfx(Cue("trivial", priority: 250, loop: true));
            Assert.IsFalse(trivial.IsValid, "a less important sound is dropped instead of cutting more important ones");
        }

        [UnityTest]
        public IEnumerator PlaySfxAt_UsesPositionAndCue3DSettings_AndResetsOnRelease()
        {
            var cue = Cue("boom", loop: true);
            Set(cue, "spatialBlend", 1f);
            Set(cue, "maxDistance", 30f);

            var handle = _manager.PlaySfxAt(cue, new Vector3(5f, 0f, 2f));
            var source = Source(handle);
            yield return null;

            Assert.AreEqual(new Vector3(5f, 0f, 2f), source.transform.position);
            Assert.AreEqual(1f, source.spatialBlend);
            Assert.AreEqual(30f, source.maxDistance);

            yield return handle.StopAsync().ToCoroutine();
            Assert.AreEqual(0f, source.spatialBlend, "pooled source goes back to 2D for the next PlaySfx");
        }

        [UnityTest]
        public IEnumerator PausedChannel_KeepsOneShotAlive_UntilResumedAndFinished()
        {
            var handle = _manager.PlaySfx(Cue("short", loop: false, seconds: 0.3f));
            _manager.SetChannelPaused(AudioChannel.Sfx, true);

            yield return WaitRealtime(0.6f); // longer than the clip
            Assert.IsTrue(handle.IsValid, "a paused sound must not be treated as finished");
            Assert.IsTrue(_manager.IsChannelPaused(AudioChannel.Sfx));

            _manager.SetChannelPaused(AudioChannel.Sfx, false);
            yield return WaitRealtime(0.8f);
            Assert.IsFalse(handle.IsValid, "after resuming it plays to the end and is released");
        }

        [UnityTest]
        public IEnumerator Duck_LowersMusic_AndDisposeRestoresIt()
        {
            var music = _manager.PlayMusic(Clip("theme", 5f), 1f, fadeDuration: 0f);
            var source = Source(music);
            yield return null;
            Assert.AreEqual(1f, source.volume, 0.01f);

            var duck = _manager.Duck(AudioChannel.Music, 0.25f, fadeDuration: 0f);
            Assert.AreEqual(0.25f, source.volume, 0.01f);

            duck.Dispose();
            Assert.AreEqual(1f, source.volume, 0.01f);
        }

        [UnityTest]
        public IEnumerator CueWithDuckMusic_DucksWhileItPlays()
        {
            var music = _manager.PlayMusic(Clip("theme", 5f), 1f, fadeDuration: 0f);
            var musicSource = Source(music);
            yield return null;

            var voice = Cue("voice", loop: true);
            Set(voice, "duckMusic", 0.4f);
            Set(voice, "duckFadeDuration", 0f);

            var line = _manager.PlaySfx(voice);
            Assert.AreEqual(0.4f, musicSource.volume, 0.01f);

            yield return line.StopAsync().ToCoroutine();
            Assert.AreEqual(1f, musicSource.volume, 0.01f, "duck released with the sound");
        }

        [UnityTest]
        public IEnumerator StopChannel_StopsOnlyThatChannel()
        {
            var music = _manager.PlayMusic(Clip("theme", 5f), 1f, fadeDuration: 0f);
            var a = _manager.PlaySfx(Cue("a", loop: true));
            var b = _manager.PlaySfx(Cue("b", loop: true));
            var ui = _manager.PlayUi(Cue("click", loop: true));
            yield return null;

            yield return _manager.StopChannelAsync(AudioChannel.Sfx).ToCoroutine();

            Assert.IsFalse(a.IsValid);
            Assert.IsFalse(b.IsValid);
            Assert.IsTrue(ui.IsValid);
            Assert.IsTrue(music.IsValid);
        }

        // ---- helpers ----

        private AudioSource Source(AudioPlaybackHandle handle)
        {
            var registry = (AudioPlaybackRegistry)Get(_manager, "_playbacks");
            Assert.IsTrue(registry.TryGet(handle.PlaybackId, out var entry), $"no playback {handle.PlaybackId}");
            return entry.Source;
        }

        private AudioClip Clip(string name, float seconds)
        {
            var clip = AudioClip.Create(name, Mathf.Max(1, (int)(44100 * seconds)), 1, 44100, false);
            _owned.Add(clip);
            return clip;
        }

        private AudioCue Cue(string name, int priority = 128, bool loop = false, float seconds = 2f)
        {
            var cue = ScriptableObject.CreateInstance<AudioCue>();
            cue.name = name;
            Set(cue, "channel", AudioChannel.Sfx);
            Set(cue, "clip", Clip(name, seconds));
            Set(cue, "loop", loop);
            Set(cue, "randomizePitch", false);
            Set(cue, "pitchRange", Vector2.one);
            Set(cue, "priority", priority);
            _owned.Add(cue);
            return cue;
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        private static object Get(object target, string name) =>
            (target.GetType().GetField(name, Flags) ?? throw new MissingFieldException(target.GetType().Name, name)).GetValue(target);

        private static void Set(object target, string name, object value) =>
            (target.GetType().GetField(name, Flags) ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);
    }
}
