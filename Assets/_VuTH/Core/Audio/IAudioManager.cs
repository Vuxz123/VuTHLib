using Cysharp.Threading.Tasks;
using _VuTH.Common;
using UnityEngine;

namespace _VuTH.Core.Audio
{
    public interface IAudioManager : ICommonManager
    {
        float MasterVolume { get; }
        float MusicVolume { get; }
        float SfxVolume { get; }
        float UiVolume { get; }
        bool Muted { get; }

        AudioPlaybackHandle PlayCue(AudioCue cue, float volumeScale = 1f);
        AudioPlaybackHandle PlayMusic(AudioCue cue, float fadeDuration = -1f);
        AudioPlaybackHandle PlayMusic(AudioClip clip, float volume = 1f, float fadeDuration = -1f);
        AudioPlaybackHandle PlaySfx(AudioCue cue, float volumeScale = 1f);
        AudioPlaybackHandle PlayUi(AudioCue cue, float volumeScale = 1f);
        UniTask StopMusicAsync(float fadeDuration = 0f);
        UniTask StopAsync(AudioPlaybackHandle handle, float fadeDuration = 0f);
        bool IsValid(AudioPlaybackHandle handle);

        void SetMuted(bool muted);
        void SetMasterVolume(float volume);
        void SetMusicVolume(float volume);
        void SetSfxVolume(float volume);
        void SetUiVolume(float volume);

        /// <summary>
        /// Plays an SFX cue at a world position, using the cue's 3D settings (spatial blend, min/max distance, rolloff).
        /// </summary>
        AudioPlaybackHandle PlaySfxAt(AudioCue cue, Vector3 position, float volumeScale = 1f);

        /// <summary>
        /// Pauses / resumes every playback of <paramref name="channel"/> (e.g. pause SFX + music while the game is
        /// paused, keep UI). Sounds started on a paused channel start paused. Paused sounds are not released.
        /// </summary>
        void SetChannelPaused(AudioChannel channel, bool paused);

        bool IsChannelPaused(AudioChannel channel);

        /// <summary>
        /// Lowers <paramref name="channel"/> to <paramref name="volumeFactor"/> (0..1) until the returned handle is
        /// disposed. Several ducks on a channel combine to the lowest factor. Fades in and out over
        /// <paramref name="fadeDuration"/> (unscaled time).
        /// </summary>
        AudioDuckHandle Duck(AudioChannel channel, float volumeFactor, float fadeDuration = 0.25f);

        /// <summary>Stops every playback of <paramref name="channel"/> (Music = <see cref="StopMusicAsync"/>).</summary>
        UniTask StopChannelAsync(AudioChannel channel, float fadeDuration = 0f);
    }
}
