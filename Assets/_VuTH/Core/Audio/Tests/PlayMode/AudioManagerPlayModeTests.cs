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
    public class AudioManagerPlayModeTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private readonly List<Object> _ownedObjects = new();
        private AudioManager _manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CleanupManagers();

            var listenerGo = new GameObject("AudioListener_Test");
            listenerGo.AddComponent<AudioListener>();
            _ownedObjects.Add(listenerGo);

            var managerGo = new GameObject("AudioManager_Test");
            _manager = managerGo.AddComponent<AudioManager>();
            _ownedObjects.Add(managerGo);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var ownedObject in _ownedObjects)
            {
                if (ownedObject != null)
                {
                    Object.Destroy(ownedObject);
                }
            }

            _ownedObjects.Clear();
            CleanupManagers();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlaySfx_WithLoopingCue_ReturnsHandle_AndStopReleasesPlayback()
        {
            var clip = CreateClip("sfx_loop", 44100);
            var cue = CreateCue(AudioChannel.Sfx, clip, loop: true, volume: 0.75f);

            var handle = _manager.PlaySfx(cue);
            yield return null;

            Assert.That(handle.IsValid, Is.True);

            var source = GetPlaybackSource(handle);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.clip, Is.SameAs(clip));
            Assert.That(source.loop, Is.True);

            yield return handle.StopAsync().ToCoroutine();

            Assert.That(handle.IsValid, Is.False);
            Assert.That(source.clip, Is.Null);
        }

        [UnityTest]
        public IEnumerator VolumeSettings_UpdateActivePlaybackVolume()
        {
            var clip = CreateClip("sfx_volume", 44100);
            var cue = CreateCue(AudioChannel.Sfx, clip, loop: true, volume: 0.8f);

            var handle = _manager.PlaySfx(cue);
            yield return null;

            var source = GetPlaybackSource(handle);
            Assert.That(source.volume, Is.EqualTo(0.8f).Within(0.001f));

            _manager.SetMasterVolume(0.5f);
            _manager.SetSfxVolume(0.25f);
            yield return null;

            Assert.That(_manager.MasterVolume, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(_manager.SfxVolume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(source.volume, Is.EqualTo(0.1f).Within(0.001f));

            _manager.SetMuted(true);
            yield return null;

            Assert.That(_manager.Muted, Is.True);
            Assert.That(source.volume, Is.EqualTo(0f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PlayMusic_WithSameClipWhilePlaying_ReusesActivePlayback()
        {
            var clip = CreateClip("music_theme", 88200);

            var firstHandle = _manager.PlayMusic(clip, volume: 0.9f, fadeDuration: 0f);
            yield return null;

            var secondHandle = _manager.PlayMusic(clip, volume: 0.3f, fadeDuration: 0f);
            yield return null;

            Assert.That(firstHandle.IsValid, Is.True);
            Assert.That(secondHandle.IsValid, Is.True);
            Assert.That(secondHandle.PlaybackId, Is.EqualTo(firstHandle.PlaybackId));

            yield return _manager.StopMusicAsync().ToCoroutine();

            Assert.That(firstHandle.IsValid, Is.False);
            Assert.That(secondHandle.IsValid, Is.False);
        }

        private AudioClip CreateClip(string name, int samples)
        {
            var clip = AudioClip.Create(name, samples, 1, 44100, false);
            _ownedObjects.Add(clip);
            return clip;
        }

        private AudioCue CreateCue(AudioChannel channel, AudioClip clip, bool loop, float volume)
        {
            var cue = ScriptableObject.CreateInstance<AudioCue>();
            SetField(cue, "channel", channel);
            SetField(cue, "clip", clip);
            SetField(cue, "loop", loop);
            SetField(cue, "volume", volume);
            SetField(cue, "pitchRange", Vector2.one);
            SetField(cue, "randomizePitch", false);
            SetField(cue, "priority", 128);
            _ownedObjects.Add(cue);
            return cue;
        }

        private AudioSource GetPlaybackSource(AudioPlaybackHandle handle)
        {
            var playbacks = (IDictionary)GetRequiredField(typeof(AudioManager), "_playbacks").GetValue(_manager);
            var entry = playbacks[handle.PlaybackId];
            Assert.That(entry, Is.Not.Null, $"Expected playback entry for handle {handle.PlaybackId}.");

            var sourceProperty = entry.GetType().GetProperty("Source", InstanceFlags);
            Assert.That(sourceProperty, Is.Not.Null, "PlaybackEntry.Source reflection lookup failed.");

            return sourceProperty.GetValue(entry) as AudioSource;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            GetRequiredField(target.GetType(), fieldName).SetValue(target, value);
        }

        private static FieldInfo GetRequiredField(Type type, string fieldName)
        {
            return type.GetField(fieldName, InstanceFlags)
                   ?? throw new MissingFieldException(type.FullName, fieldName);
        }

        private static void CleanupManagers()
        {
            foreach (var manager in Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None))
            {
                if (manager)
                {
                    Object.DestroyImmediate(manager.gameObject);
                }
            }
        }
    }
}
