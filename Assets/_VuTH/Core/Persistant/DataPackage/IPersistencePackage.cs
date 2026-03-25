namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Interface for persistence packages that manage save data.
    /// Aggregates multiple PersistentField and handles save/load logic.
    /// </summary>
    public interface IPersistencePackage
    {
        /// <summary>
        /// Unique key for this package in storage.
        /// </summary>
        string StorageKey { get; }
        
        /// <summary>
        /// Current save strategy for this package.
        /// </summary>
        SaveStrategy Strategy { get; }
        
        /// <summary>
        /// Whether this package has unsaved changes.
        /// </summary>
        bool IsDirty { get; }
        
        /// <summary>
        /// Mark this package as having unsaved changes.
        /// </summary>
        void MarkDirty();
        
        /// <summary>
        /// Force save immediately.
        /// </summary>
        void SaveNow();
        
        /// <summary>
        /// Load data from storage.
        /// </summary>
        void Load();
    }
    
    /// <summary>
    /// Generic interface for typed persistence packages.
    /// </summary>
    public interface IPersistencePackage<TData> : IPersistencePackage where TData : class
    {
        /// <summary>
        /// Extract current data as DTO for serialization.
        /// </summary>
        TData ExtractPayload();
        
        /// <summary>
        /// Inject data from DTO after deserialization.
        /// </summary>
        void InjectPayload(TData data);
    }
}
