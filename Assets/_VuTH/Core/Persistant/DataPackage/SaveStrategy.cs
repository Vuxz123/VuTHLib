namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Defines the save strategy for persistence packages.
    /// Controls when data is persisted to storage.
    /// </summary>
    public enum SaveStrategy
    {
        /// <summary>
        /// Save immediately when data changes.
        /// Use for sensitive data like IAP, card top-ups.
        /// </summary>
        Immediate,
        
        /// <summary>
        /// Debounced save - waits for X seconds without changes before saving.
        /// Optimizes I/O for gameplay data like Gold, Exp.
        /// </summary>
        Debounced,
        
        /// <summary>
        /// Only save when explicitly called (Checkpoints, End Level).
        /// </summary>
        ManualOnly,
        
        /// <summary>
        /// Only save when app is closed or paused (Settings).
        /// </summary>
        OnAppClose
    }
}
