#nullable enable
using System;
using _VuTH.Core.Persistent.DataPackage;
using UnityEngine;

namespace _VuTH.Core.Audio
{
    [Serializable]
    public sealed class AudioSettingsPayload
    {
        public bool muted;
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 1f;
    }

    /// <summary>
    /// Audio settings. The fields (<c>Muted</c>, <c>MasterVolume</c>, …) and the payload mapping are generated from
    /// <see cref="AudioSettingsPayload"/> ([GeneratePersistentFields]); its <c>[Range(0, 1)]</c> makes the volumes
    /// <see cref="RangedPersistentField{T}"/>s clamped to 0..1, whether set by the game or loaded from a save.
    /// </summary>
    [GeneratePersistentFields]
    public sealed partial class AudioSettingsPackage : PersistencePackage<AudioSettingsPayload>
    {
        public override float DebounceSeconds => 0.75f;

        public AudioSettingsPackage()
            : this(false, 1f, 1f, 1f, 1f)
        {
        }

        public AudioSettingsPackage(
            bool muted = false,
            float masterVolume = 1f,
            float musicVolume = 1f,
            float sfxVolume = 1f,
            float uiVolume = 1f)
            : base("audio_settings", SaveStrategy.Debounced)
        {
            // Initial values (e.g. the AudioManager defaults); the generated fields clamp them.
            Muted.SetValueWithoutMarkingDirty(muted);
            MasterVolume.SetValueWithoutMarkingDirty(masterVolume);
            MusicVolume.SetValueWithoutMarkingDirty(musicVolume);
            SfxVolume.SetValueWithoutMarkingDirty(sfxVolume);
            UiVolume.SetValueWithoutMarkingDirty(uiVolume);
        }
    }
}
