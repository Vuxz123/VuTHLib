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
        T? GetPackage<T>() where T : class, IPersistencePackage;
        bool TryGetPackage<T>(out T? package) where T : class, IPersistencePackage;
        void SaveAll();
        UniTask SaveAllAsync();
        void LoadAll(bool discardUnsavedChanges = false);
        UniTask LoadAllAsync(bool discardUnsavedChanges = false);

        /// <summary>Completes when the initially configured packages finished loading.</summary>
        UniTask WaitUntilReadyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the stored data of every package (shared ones, and slot-scoped ones in the active slot) and resets
        /// them to their defaults. For "reset progress" or a player's data deletion request.
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
        new bool IsInitialized { get; }
        int PackageCount { get; }
    }
}
