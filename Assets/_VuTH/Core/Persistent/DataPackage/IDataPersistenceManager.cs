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
        T GetPackage<T>() where T : class, IPersistencePackage;
        bool TryGetPackage<T>(out T package) where T : class, IPersistencePackage;
        void SaveAll();
        UniTask SaveAllAsync();
        void LoadAll(bool discardUnsavedChanges = false);
        UniTask LoadAllAsync(bool discardUnsavedChanges = false);

        /// <summary>Completes when the initially configured packages finished loading.</summary>
        UniTask WaitUntilReadyAsync(CancellationToken cancellationToken = default);
        new bool IsInitialized { get; }
        int PackageCount { get; }
    }
}
