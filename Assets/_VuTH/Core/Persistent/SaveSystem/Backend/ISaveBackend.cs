#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistent.SaveSystem.Backend
{
    /// <summary>
    /// Optional write ordering across preprocessing and storage. Tickets belong to this backend instance;
    /// reserve before asynchronous encryption, then pass the ticket to the write. A completed newer write
    /// must not be replaced by an older ticket for the same key. All write entry points share this ordering.
    /// </summary>
    public interface IOrderedSaveBackend : ISaveBackend
    {
        long ReserveWrite();
        UniTask SaveRawOrderedAsync(string key, string data, long ticket, CancellationToken cancellationToken = default);
    }

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
