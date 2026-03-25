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
    }
}
