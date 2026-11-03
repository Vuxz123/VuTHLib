using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Events.MessagePipe;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Audio.Tests.PlayMode
{
    /// <summary>Music transitions that overlap (stop-while-fading, interrupted crossfades) and screen music rules.</summary>
    public class AudioMusicTransitionPlayModeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private readonly List<Object> _owned = new();
        private AudioManager _manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyManagers();
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
            DestroyManagers();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayMusic_RightAfterFadingStop_IsNotSilencedByTheOldFade()
        {
            var first = _manager.PlayMusic(Clip("first"), 1f, fadeDuration: 0f);
            yield return null;
            Assert.IsTrue(first.IsValid);

            _manager.StopMusicAsync(fadeDuration: 0.3f).Forget();
            var second = _manager.PlayMusic(Clip("second"), 1f, fadeDuration: 0f);
            var source = PlaybackSource(second);

            yield return WaitRealtime(0.6f); // the old fade-out has long finished

            Assert.IsTrue(second.IsValid);
            Assert.AreEqual("second", source.clip.name);
            Assert.That(source.volume, Is.EqualTo(1f).Within(0.01f), "new music must not inherit the old fade-out");
        }

        [UnityTest]
        public IEnumerator InterruptedCrossFade_OutgoingMusicNeverJumpsBackUp()
        {
            _manager.PlayMusic(Clip("a"), 1f, fadeDuration: 0f);
            yield return null;

            var b = _manager.PlayMusic(Clip("b"), 1f, fadeDuration: 1f);
            var bSource = PlaybackSource(b);
            yield return WaitRealtime(0.4f);
            var bVolumeAtInterrupt = bSource.volume;
            Assert.That(bVolumeAtInterrupt, Is.InRange(0.05f, 0.95f), "precondition: b is mid fade-in");

            _manager.PlayMusic(Clip("c"), 1f, fadeDuration: 1f);

            var max = 0f;
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 0.4f)
            {
                yield return null;
                if (bSource.clip != null && bSource.clip.name == "b") max = Mathf.Max(max, bSource.volume);
            }

            Assert.LessOrEqual(max, bVolumeAtInterrupt + 0.02f, "outgoing music must keep fading down from where it was");
        }

        [UnityTest]
        public IEnumerator ScreenWithoutMusic_KeepsCurrentMusic_ByDefault()
        {
            var music = _manager.PlayMusic(Clip("theme"), 1f, fadeDuration: 0f);
            yield return null;

            EnterScreen(Screen("Popup"));
            yield return WaitRealtime(0.6f);

            Assert.IsTrue(music.IsValid, "a popup / pushed screen without music must not cut the current music");
        }

        [UnityTest]
        public IEnumerator ScreenWithoutMusic_StopsMusic_WhenRequested()
        {
            var music = _manager.PlayMusic(Clip("theme"), 1f, fadeDuration: 0f);
            yield return null;

            var silent = Screen("Silent");
            silent.stopMusicIfNoBackgroundMusic = true;
            EnterScreen(silent);
            yield return WaitRealtime(0.8f);

            Assert.IsFalse(music.IsValid);
        }

        [UnityTest]
        public IEnumerator ScreenMusicEntry_OverridesBackgroundClip()
        {
            var screen = Screen("Home");
            screen.backgroundMusic = Clip("clip_on_screen_model");
            var cue = Cue(Clip("cue_music"));
            SetField(_manager, "screenMusic", new[] { new ScreenMusicEntry { screen = screen, music = cue } });

            EnterScreen(screen);
            yield return null;

            var active = (AudioSource)GetField(_manager, "_activeMusicSource");
            Assert.IsNotNull(active);
            Assert.AreEqual("cue_music", active.clip.name);
        }

        // ---- helpers ----

        private void EnterScreen(ScreenModel screen)
        {
            typeof(AudioManager).GetMethod("HandlePostScreenEnter", Flags)!
                .Invoke(_manager, new object[] { new PostScreenEnterEvent(null, screen) });
        }

        private AudioSource PlaybackSource(AudioPlaybackHandle handle)
        {
            var registry = (AudioPlaybackRegistry)GetField(_manager, "_playbacks");
            Assert.IsTrue(registry.TryGet(handle.PlaybackId, out var entry));
            return entry.Source;
        }

        private AudioClip Clip(string name)
        {
            var clip = AudioClip.Create(name, 44100 * 5, 1, 44100, false);
            _owned.Add(clip);
            return clip;
        }

        private ScreenModel Screen(string name)
        {
            var screen = ScriptableObject.CreateInstance<ScreenModel>();
            screen.name = name;
            _owned.Add(screen);
            return screen;
        }

        private AudioCue Cue(AudioClip clip)
        {
            var cue = ScriptableObject.CreateInstance<AudioCue>();
            SetField(cue, "channel", AudioChannel.Music);
            SetField(cue, "clip", clip);
            SetField(cue, "loop", true);
            SetField(cue, "randomizePitch", false);
            _owned.Add(cue);
            return cue;
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        private static object GetField(object target, string name) =>
            (target.GetType().GetField(name, Flags) ?? throw new MissingFieldException(target.GetType().Name, name)).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            (target.GetType().GetField(name, Flags) ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);

        private static void DestroyManagers()
        {
            foreach (var m in Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None))
            {
                if (m) Object.DestroyImmediate(m.gameObject);
            }
        }
    }
}
