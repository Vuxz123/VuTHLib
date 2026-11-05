#nullable enable
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
        /// Key the package's data is stored under: <see cref="BaseStorageKey"/>, prefixed with the active save slot for a
        /// slot-scoped package.
        /// </summary>
        string StorageKey { get; }

        /// <summary>
        /// The key given by the package, without save slot.
        /// </summary>
        string BaseStorageKey { get; }

        /// <summary>
        /// Whether the package's data belongs to a save slot (e.g. game progress), so each slot has its own copy.
        /// Packages that are not (e.g. settings) keep one copy whatever the slot. See <see cref="IDataPersistenceManager.SwitchSlotAsync"/>.
        /// </summary>
        bool IsSlotScoped { get; }

        /// <summary>
        /// Points <see cref="StorageKey"/> to <paramref name="slot"/> (null: the default slot). Called by the manager,
        /// which saves before and loads after. When the slot changes, the package is reset to its defaults (the data in
        /// memory belongs to the previous slot). Throws for a package that is not slot scoped.
        /// </summary>
        void SetSlot(string? slot);
        
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
        /// Changes made before that are overwritten by the loaded data. Throws if the package cannot load at all
        /// (no save service).
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

        /// <summary>
        /// Deletes the stored data (and its corrupted backup) and resets the package to its defaults, without saving.
        /// Re-enables saving if a failed load had disabled it.
        /// </summary>
        UniTask DeleteAsync();
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
