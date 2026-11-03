namespace _VuTH.Core.Audio
{
    /// <summary>How 0..1 settings sliders (master / channel volumes) are turned into an output gain.</summary>
    public enum AudioVolumeCurve
    {
        /// <summary>gain = slider. Simple, but the top half of the slider sounds almost the same.</summary>
        Linear = 0,

        /// <summary>gain = slider². Closer to how loudness is perceived (0.5 ≈ -12 dB), so the slider feels even.</summary>
        Perceptual = 1,
    }
}
