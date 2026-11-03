using UnityEngine;
using UnityEngine.Audio;

namespace _VuTH.Core.Audio
{
    [CreateAssetMenu(fileName = "AudioCue", menuName = "VuTH/Audio/Audio Cue")]
    public class AudioCue : ScriptableObject
    {
        [Header("Routing")]
        [SerializeField] private AudioChannel channel = AudioChannel.Sfx;
        [SerializeField] private AudioMixerGroup outputMixerGroup;

        [Header("Clip")]
        [SerializeField] private AudioClip clip;
        [SerializeField] private bool loop;
        [SerializeField] private bool ignoreListenerPause;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private Vector2 pitchRange = Vector2.one;
        [SerializeField] private bool randomizePitch = true;
        [SerializeField, Range(0, 256)] private int priority = 128;

        [Header("3D (used by PlaySfxAt)")]
        [Tooltip("0 = 2D, 1 = fully 3D. Only meaningful when played at a position.")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField, Min(0f)] private float minDistance = 1f;
        [SerializeField, Min(0f)] private float maxDistance = 50f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [Header("Ducking")]
        [Tooltip("Music volume factor while this sound plays (1 = no ducking, 0.3 = duck music to 30%).")]
        [SerializeField, Range(0f, 1f)] private float duckMusic = 1f;
        [SerializeField, Min(0f)] private float duckFadeDuration = 0.15f;

        public AudioChannel Channel => channel;
        public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
        public AudioClip Clip => clip;
        public bool Loop => loop;
        public bool IgnoreListenerPause => ignoreListenerPause;
        public float Volume => volume;
        public Vector2 PitchRange => pitchRange;
        public bool RandomizePitch => randomizePitch;
        public int Priority => priority;
        public float SpatialBlend => spatialBlend;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public AudioRolloffMode RolloffMode => rolloffMode;
        public float DuckMusic => duckMusic;
        public float DuckFadeDuration => duckFadeDuration;

        public bool IsValid => clip != null;

        private void OnValidate()
        {
            if (pitchRange.x <= 0f)
            {
                pitchRange.x = 0.01f;
            }

            if (pitchRange.y <= 0f)
            {
                pitchRange.y = 0.01f;
            }

            if (pitchRange.y < pitchRange.x)
            {
                pitchRange.y = pitchRange.x;
            }

            if (maxDistance < minDistance)
            {
                maxDistance = minDistance;
            }
        }
    }
}
