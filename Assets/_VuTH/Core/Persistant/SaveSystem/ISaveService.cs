#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// Main save service contract exposed to DataManager and other consumers.
    /// Handles serialization, encryption, and versioning transparently.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>
        /// Saves data asynchronously with automatic versioning.
        /// Pipeline: Serialize -> Encrypt -> Save.
        /// </summary>
        UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads data asynchronously with automatic migration and decryption.
        /// Pipeline: Load -> Decrypt -> Deserialize -> Migrate if needed.
        /// Returns defaultValue if key not found or error occurs.
        /// </summary>
        UniTask<T> LoadAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);

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
