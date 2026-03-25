using _VuTH.Common;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Interface for DataPersistenceManager.
    /// </summary>
    public interface IDataPersistenceManager : ICommonManager
    {
        void RegisterPackage(IPersistencePackage package);
        void UnregisterPackage(IPersistencePackage package);
        T GetPackage<T>() where T : class, IPersistencePackage;
        bool TryGetPackage<T>(out T package) where T : class, IPersistencePackage;
        void SaveAll();
        UniTask SaveAllAsync();
        void LoadAll();
        UniTask LoadAllAsync();
        bool IsInitialized { get; }
        int PackageCount { get; }
    }
}
