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
        [SerializeField, Range(0, 256)] private int priority = 128;

        public AudioChannel Channel => channel;
        public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
        public AudioClip Clip => clip;
        public bool Loop => loop;
        public bool IgnoreListenerPause => ignoreListenerPause;
        public float Volume => volume;
        public Vector2 PitchRange => pitchRange;
        public int Priority => priority;

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
        }
    }
}
