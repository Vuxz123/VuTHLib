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
    /// <para>
    /// What only the manager may do (moving a package to another slot, giving it its save service) is not part of this
    /// interface: game code cannot bypass the manager.
    /// </para>
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
        /// Raised when a load leaves <see cref="CanSave"/> false: the stored data cannot be read and changes will not be
        /// saved. Tell the player (e.g. "your save could not be loaded, contact support") instead of letting them play on
        /// and lose their progress.
        /// </summary>
        event System.Action<IPersistencePackage> SaveBlocked;

        /// <summary>
        /// Raised when writing the data failed (disk full, I/O error). The package stays dirty and retries on its own
        /// with a growing delay, as well as on the next change and on pause / quit.
        /// </summary>
        event System.Action<IPersistencePackage, System.Exception> SaveFailed;

        /// <summary>
        /// Completes when the first load finished, whatever its outcome (see <see cref="LoadStatus"/>).
        /// Changes made before that are overwritten by the loaded data. Throws if the package cannot load at all
        /// (no save service).
        /// </summary>
        UniTask WaitUntilLoadedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Mark the package as dirty and notify the save pipeline.
        /// </summary>
        void MarkDirty();

        /// <summary>
        /// Force save and await completion.
        /// </summary>
        UniTask SaveNowAsync();

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
        TData ExtractPayload();

        /// <summary>
        /// Inject data from DTO after deserialization.
        /// </summary>
        void InjectPayload(TData data);
    }

    /// <summary>
    /// What the manager does to a package: give it its save service, move it to another save slot. Internal, so game
    /// code goes through <see cref="IDataPersistenceManager"/>: a package moved to a slot behind the manager's back
    /// would not be saved before nor loaded after.
    /// </summary>
    internal interface IManagedPersistencePackage : IPersistencePackage
    {
        void SetSaveService(ISaveService saveService);

        /// <summary>
        /// Points <see cref="IPersistencePackage.StorageKey"/> to <paramref name="slot"/> (null: the default slot). The
        /// manager saves before and loads after. When the slot changes, the package is reset to its defaults (the data
        /// in memory belongs to the previous slot). Throws for a package that is not slot scoped.
        /// </summary>
        void SetSlot(string? slot);
    }
}
