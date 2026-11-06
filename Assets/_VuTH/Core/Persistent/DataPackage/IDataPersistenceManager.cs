#nullable enable
using System.Threading;
using _VuTH.Common;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Interface for DataPersistenceManager. <see cref="GetPackage{T}"/> matches the exact type first, then any package
    /// assignable to <c>T</c>.
    /// </summary>
    public interface IDataPersistenceManager : ICommonManager
    {
        void RegisterPackage(IPersistencePackage package);
        void UnregisterPackage(IPersistencePackage package);
        /// <summary>The package of type <typeparamref name="T"/>; logs a warning when there is none.</summary>
        T? GetPackage<T>() where T : class, IPersistencePackage;

        /// <summary>The package of type <typeparamref name="T"/>, if any (no warning when there is none).</summary>
        bool TryGetPackage<T>(out T? package) where T : class, IPersistencePackage;
        void SaveAll();
        UniTask SaveAllAsync();
        void LoadAll(bool discardUnsavedChanges = false);
        UniTask LoadAllAsync(bool discardUnsavedChanges = false);

        /// <summary>Completes when the initially configured packages finished loading.</summary>
        UniTask WaitUntilReadyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the stored data of every package (shared ones, and slot-scoped ones in the active slot) and resets
        /// them to their defaults. For "reset progress" or a player's data deletion request. Waits for a slot switch in
        /// progress. A package that fails does not stop the others: the failures are thrown together at the end
        /// (<see cref="System.AggregateException"/>).
        /// </summary>
        UniTask DeleteAllAsync();

        /// <summary>The save slot of slot-scoped packages; null is the default slot.</summary>
        string? ActiveSlot { get; }

        /// <summary>
        /// Saves the pending changes of slot-scoped packages to the current slot, then points them to
        /// <paramref name="slot"/> and loads it. Packages that are not slot scoped are untouched.
        /// </summary>
        UniTask SwitchSlotAsync(string? slot, CancellationToken cancellationToken = default);

        /// <summary>Deletes the data of slot-scoped packages in <paramref name="slot"/> (resetting them if it is active).</summary>
        UniTask DeleteSlotAsync(string? slot);
        /// <summary>
        /// Raised when a package's load leaves it unable to save (<see cref="IPersistencePackage.CanSave"/> false): its
        /// stored data could not be read. Tell the player instead of letting the changes pile up and be lost.
        /// </summary>
        event System.Action<IPersistencePackage> OnSaveBlocked;

        /// <summary>
        /// Raised when a package's save failed (<see cref="IPersistencePackage.SaveFailed"/>); the package retries on its
        /// own. Use it to warn the player when the device is out of storage.
        /// </summary>
        event System.Action<IPersistencePackage, System.Exception> OnSaveFailed;
        int PackageCount { get; }
    }
}
