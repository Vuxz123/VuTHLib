namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Optional capability: a package that can flush its dirty data synchronously.
    /// <see cref="SaveLifecycleHook"/> uses it on app pause / quit, where async saves may never complete.
    /// <see cref="PersistencePackage{TData}"/> implements it.
    /// </summary>
    public interface ISyncPersistencePackage
    {
        /// <summary>
        /// Saves now on the calling thread if the package is dirty.
        /// Returns false when nothing was written synchronously (not dirty, no save service,
        /// or the backend has no sync path) — callers may then fall back to <see cref="IPersistencePackage.SaveNowAsync"/>.
        /// </summary>
        bool TrySaveImmediate();
    }
}
