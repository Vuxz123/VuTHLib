using System.Threading;
using _VuTH.Core.Persistent.SaveSystem;
using Cysharp.Threading.Tasks;
using R3;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Interface for persistence packages that manage save data.
    /// Packages only hold data — save logic is managed by DataPersistenceManager.
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
        /// Observable of dirty state changes. Manager subscribes to drive save pipeline.
        /// </summary>
        Observable<bool> DirtyObservable { get; }
        
        /// <summary>
        /// Save interval in seconds for the <see cref="SaveStrategy.Debounced"/> strategy.
        /// </summary>
        float DebounceSeconds { get; }

        /// <summary>
        /// Outcome of the last load, or null before the first load completed.
        /// </summary>
        SaveLoadStatus? LoadStatus { get; }

        /// <summary>
        /// False when the last load could not confirm that the stored data may be overwritten (unreadable storage,
        /// newer schema version, failed migration). Saves are then refused, so that data is not lost.
        /// </summary>
        bool CanSave { get; }

        /// <summary>
        /// Completes when the first load finished, whatever its outcome (see <see cref="LoadStatus"/>).
        /// Changes made before that are overwritten by the loaded data.
        /// </summary>
        UniTask WaitUntilLoadedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Set the save service used by this package.
        /// </summary>
        void SetSaveService(ISaveService saveService);

        /// <summary>
        /// Mark the package as dirty and notify the save pipeline.
        /// </summary>
        void MarkDirty();
        
        /// <summary>
        /// Extract current data as DTO for serialization.
        /// </summary>
        object ExtractPayload();
        
        /// <summary>
        /// Inject data from DTO after deserialization.
        /// </summary>
        void InjectPayload(object data);
        
        /// <summary>
        /// Force save immediately.
        /// </summary>
        void SaveNow();

        /// <summary>
        /// Force save and await completion.
        /// </summary>
        UniTask SaveNowAsync();
        
        /// <summary>
        /// Load data from storage.
        /// </summary>
        void Load();

        /// <summary>
        /// Load data from storage and await completion.
        /// </summary>
        UniTask LoadAsync();
    }
    
    /// <summary>
    /// Generic interface for typed persistence packages.
    /// </summary>
    public interface IPersistencePackage<TData> : IPersistencePackage where TData : class
    {
        /// <summary>
        /// Extract current data as DTO for serialization.
        /// </summary>
        new TData ExtractPayload();
        
        /// <summary>
        /// Inject data from DTO after deserialization.
        /// </summary>
        void InjectPayload(TData data);
    }
}
