#nullable enable
using System;
using _VuTH.Core.Persistant.DataPackage;
using UnityEngine;

namespace _VuTH.Core.Audio
{
    [Serializable]
    public sealed class AudioSettingsPayload
    {
        public bool muted;
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public float uiVolume = 1f;
    }

    public sealed class AudioSettingsPackage : PersistencePackage<AudioSettingsPayload>
    {
        public override float DebounceSeconds => 0.75f;

        public PersistentField<bool> Muted { get; }
        public PersistentField<float> MasterVolume { get; }
        public PersistentField<float> MusicVolume { get; }
        public PersistentField<float> SfxVolume { get; }
        public PersistentField<float> UiVolume { get; }

        public AudioSettingsPackage(
            bool muted = false,
            float masterVolume = 1f,
            float musicVolume = 1f,
            float sfxVolume = 1f,
            float uiVolume = 1f)
            : base("audio_settings", SaveStrategy.Debounced)
        {
            Muted = new PersistentField<bool>(this, muted);
            MasterVolume = new PersistentField<float>(this, Mathf.Clamp01(masterVolume));
            MusicVolume = new PersistentField<float>(this, Mathf.Clamp01(musicVolume));
            SfxVolume = new PersistentField<float>(this, Mathf.Clamp01(sfxVolume));
            UiVolume = new PersistentField<float>(this, Mathf.Clamp01(uiVolume));
        }

        public override AudioSettingsPayload ExtractPayload()
        {
            return new AudioSettingsPayload
            {
                muted = Muted.Value,
                masterVolume = MasterVolume.Value,
                musicVolume = MusicVolume.Value,
                sfxVolume = SfxVolume.Value,
                uiVolume = UiVolume.Value
            };
        }

        public override void InjectPayload(AudioSettingsPayload data)
        {
            if (data == null)
            {
                return;
            }

            LoadWithoutNotify(() =>
            {
                Muted.SetValueWithoutNotify(data.muted);
                MasterVolume.SetValueWithoutNotify(Mathf.Clamp01(data.masterVolume));
                MusicVolume.SetValueWithoutNotify(Mathf.Clamp01(data.musicVolume));
                SfxVolume.SetValueWithoutNotify(Mathf.Clamp01(data.sfxVolume));
                UiVolume.SetValueWithoutNotify(Mathf.Clamp01(data.uiVolume));
            });
        }

        public override void Dispose()
        {
            Muted.Dispose();
            MasterVolume.Dispose();
            MusicVolume.Dispose();
            SfxVolume.Dispose();
            UiVolume.Dispose();
            base.Dispose();
        }
    }
}
