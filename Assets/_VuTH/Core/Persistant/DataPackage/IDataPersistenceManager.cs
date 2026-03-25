using _VuTH.Common;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Interface for DataPersistenceManager.
    /// </summary>
    public interface IDataPersistenceManager : ICommonManager
    {
        void RegisterPackage(IPersistencePackage package);
        void UnregisterPackage(IPersistencePackage package);
        void SaveAll();
        void LoadAll();
        bool IsInitialized { get; }
        int PackageCount { get; }
    }
}