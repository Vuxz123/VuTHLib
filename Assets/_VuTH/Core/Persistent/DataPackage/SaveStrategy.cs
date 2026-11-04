namespace _VuTH.Core.Persistent.DataPackage
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
        /// Saves at most once every <see cref="IPersistencePackage.DebounceSeconds"/>: the first change starts the timer and
        /// the latest data is saved when it ends, so data that keeps changing is still saved regularly. Dirty data is also
        /// flushed on app pause / quit. Optimizes I/O for gameplay data like Gold, Exp.
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
