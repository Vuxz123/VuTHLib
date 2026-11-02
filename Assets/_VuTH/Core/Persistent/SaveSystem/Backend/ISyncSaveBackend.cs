#nullable enable

namespace _VuTH.Core.Persistent.SaveSystem.Backend
{
    /// <summary>
    /// Optional capability for backends that can persist data synchronously on the calling thread.
    /// Used for last-chance flushes (app pause / quit) where the process may be killed before
    /// an async continuation runs. Implement it alongside <see cref="ISaveBackend"/>.
    /// </summary>
    public interface ISyncSaveBackend
    {
        /// <summary>
        /// Writes raw data and returns only once it is persisted. Throws on failure.
        /// </summary>
        void SaveRaw(string key, string data);
    }
}
