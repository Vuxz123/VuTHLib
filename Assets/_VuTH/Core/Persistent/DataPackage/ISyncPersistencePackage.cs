namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>What a synchronous save (<see cref="ISyncPersistencePackage.SaveImmediate"/>) did, and why.</summary>
    public enum SyncSaveResult
    {
        /// <summary>The data was written.</summary>
        Written,

        /// <summary>Nothing to write: the package is not dirty.</summary>
        NotNeeded,

        /// <summary>Nothing worth writing: the data is about to be replaced by the load in progress.</summary>
        Loading,

        /// <summary>Saving is disabled on purpose (<see cref="IPersistencePackage.CanSave"/> false): no fallback.</summary>
        Blocked,

        /// <summary>The package cannot save at all (no save service, or disposed).</summary>
        NotReady,

        /// <summary>
        /// The save service or its backend has no synchronous path: the only case where an async save is worth
        /// trying, knowing it only completes if the app keeps running.
        /// </summary>
        Unsupported,

        /// <summary>
        /// The write threw (I/O error, disk full). Reported through <see cref="IPersistencePackage.SaveFailed"/>, which
        /// schedules the retry: do not save again.
        /// </summary>
        Failed,
    }

    /// <summary>
    /// Optional capability: a package that can flush its dirty data synchronously.
    /// <see cref="SaveLifecycleHook"/> uses it on app pause / quit, where async saves may never complete.
    /// <see cref="PersistencePackage{TData}"/> implements it.
    /// </summary>
    public interface ISyncPersistencePackage
    {
        /// <summary>Saves now on the calling thread if the package is dirty, and says what happened.</summary>
        SyncSaveResult SaveImmediate();

        /// <summary>
        /// <see cref="SaveImmediate"/> as a bool: true when the data was written. Only fall back to an async save when
        /// <see cref="SaveImmediate"/> returns <see cref="SyncSaveResult.Unsupported"/>.
        /// </summary>
        bool TrySaveImmediate();
    }
}
