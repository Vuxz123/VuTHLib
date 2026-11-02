#nullable enable

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// Optional synchronous save path (Serialize → Encrypt → write) for app pause / quit,
    /// where awaiting is not possible because the OS may terminate the process immediately afterwards.
    /// </summary>
    public interface ISyncSaveService
    {
        /// <summary>
        /// Saves synchronously. Returns false when the configured backend has no synchronous path
        /// (it does not implement <see cref="Backend.ISyncSaveBackend"/>) or the service is not initialized;
        /// callers should then fall back to the async API. Throws if the write itself fails.
        /// </summary>
        bool TrySaveImmediate<T>(string key, T data);
    }
}
