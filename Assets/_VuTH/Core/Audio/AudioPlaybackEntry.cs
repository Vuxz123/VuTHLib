using System.Threading;
using UnityEngine;

namespace _VuTH.Core.Audio
{
    /// <summary>
    /// Runtime state of one active playback tracked by <see cref="AudioPlaybackRegistry"/>:
    /// the source it plays on, its channel, its base volume, its current fade factor and its cancellation token.
    /// </summary>
    public sealed class AudioPlaybackEntry
    {
        internal AudioPlaybackEntry(int playbackId, AudioSource source, AudioChannel channel, float baseVolume, bool isMusic)
        {
            PlaybackId = playbackId;
            Source = source;
            Channel = channel;
            BaseVolume = baseVolume;
            IsMusic = isMusic;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public int PlaybackId { get; }
        public AudioSource Source { get; }
        public AudioChannel Channel { get; }
        public float BaseVolume { get; }
        public bool IsMusic { get; }
        public float FadeFactor { get; set; } = 1f;

        /// <summary>Unity priority of the sound (0 = most important). Used for voice stealing.</summary>
        public int Priority { get; internal set; } = 128;

        /// <summary><c>Time.realtimeSinceStartup</c> when the playback started (voice stealing tie-break).</summary>
        public float StartedAt { get; internal set; }

        /// <summary>Duck applied while this playback is alive (from <see cref="AudioCue.DuckMusic"/>); released with it.</summary>
        internal System.IDisposable AutoDuck { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; }
    }
}
