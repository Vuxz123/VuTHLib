using UnityEngine;

namespace _VuTH.Core.Audio
{
    /// <summary>
    /// Pure volume math used by <see cref="AudioManager"/>: resolves the effective output volume of a playback
    /// from its requested volume, the mute flag, the master volume and the volume of its channel.
    /// </summary>
    public static class AudioVolumeMixer
    {
        /// <summary>
        /// Returns the channel volume that applies to <paramref name="channel"/>.
        /// Any channel other than <see cref="AudioChannel.Music"/> and <see cref="AudioChannel.Ui"/> uses the SFX volume.
        /// </summary>
        public static float SelectChannelVolume(AudioChannel channel, float musicVolume, float sfxVolume, float uiVolume)
        {
            return channel switch
            {
                AudioChannel.Music => musicVolume,
                AudioChannel.Ui => uiVolume,
                _ => sfxVolume
            };
        }

        /// <summary>
        /// Resolves the effective volume: 0 when muted, otherwise
        /// clamp01(requested) * clamp01(channel volume) * clamp01(master volume).
        /// </summary>
        public static float Resolve(
            AudioChannel channel,
            float requestedVolume,
            bool muted,
            float masterVolume,
            float musicVolume,
            float sfxVolume,
            float uiVolume)
        {
            if (muted)
            {
                return 0f;
            }

            var channelVolume = SelectChannelVolume(channel, musicVolume, sfxVolume, uiVolume);

            return Mathf.Clamp01(requestedVolume) * Mathf.Clamp01(channelVolume) * Mathf.Clamp01(masterVolume);
        }
    }
}
