using _VuTH.Common.MessagePipe.Attributes;
using UnityEngine;

namespace _VuTH.Core.Audio.Events.MessagePipe
{
    [MessagePipeEvent]
    public sealed class PlayAudioCueEvent
    {
        public PlayAudioCueEvent(AudioCue cue, float volumeScale = 1f)
        {
            Cue = cue;
            VolumeScale = Mathf.Max(0f, volumeScale);
        }

        public AudioCue Cue { get; }
        public float VolumeScale { get; }
    }
}
