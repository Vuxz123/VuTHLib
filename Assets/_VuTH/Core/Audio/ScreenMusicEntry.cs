using System;
using _VuTH.Core.GameCycle.Screen.Core;
using UnityEngine;

namespace _VuTH.Core.Audio
{
    /// <summary>
    /// Background music for a screen as an <see cref="AudioCue"/> (mixer group, volume, loop, pitch...).
    /// Takes precedence over <see cref="ScreenModel.backgroundMusic"/>, which can only hold a raw AudioClip because the
    /// Screen assembly cannot reference Audio types.
    /// </summary>
    [Serializable]
    public struct ScreenMusicEntry
    {
        public ScreenModel screen;
        public AudioCue music;

        [Tooltip("<= 0 = AudioManager default music fade duration.")]
        public float fadeDuration;
    }
}
