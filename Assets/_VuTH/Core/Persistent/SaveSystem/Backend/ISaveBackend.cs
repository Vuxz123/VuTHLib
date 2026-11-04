#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistent.SaveSystem.Backend
{
    /// <summary>
    /// Backend contract for storage operations.
    /// Only handles raw string data - no serialization or encryption.
    /// </summary>
    public interface ISaveBackend
    {
        /// <summary>
        /// Saves raw string data asynchronously. Throws on failure.
        /// </summary>
        UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads raw string data asynchronously. Returns null only when the key does not exist;
        /// throws when the storage cannot be read, so a read error is never mistaken for "no save".
        /// </summary>
        UniTask<string?> LoadRawAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a key exists in storage.
        /// </summary>
        UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes data by key.
        /// </summary>
        UniTask DeleteAsync(string key, CancellationToken cancellationToken = default);
    }
}
