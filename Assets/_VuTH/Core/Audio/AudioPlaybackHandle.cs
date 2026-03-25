using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Audio
{
    public readonly struct AudioPlaybackHandle
    {
        public static AudioPlaybackHandle Invalid => default;

        private readonly IAudioManager _manager;

        internal AudioPlaybackHandle(IAudioManager manager, int playbackId)
        {
            _manager = manager;
            PlaybackId = playbackId;
        }

        public int PlaybackId { get; }

        public bool IsValid => _manager != null && _manager.IsValid(this);

        public UniTask StopAsync(float fadeDuration = 0f)
        {
            return _manager == null
                ? UniTask.CompletedTask
                : _manager.StopAsync(this, fadeDuration);
        }
    }
}
