#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// Main save service contract exposed to DataManager and other consumers.
    /// Handles serialization, encryption, and versioning transparently.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>
        /// Saves data asynchronously with automatic versioning.
        /// Pipeline: Serialize -> Encrypt -> Save. Throws if the save fails.
        /// </summary>
        UniTask SaveAsync<T>(string key, T data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads data and reports what happened: loaded, not found, corrupted (backed up), unreadable, newer schema
        /// or failed migration. Use <see cref="SaveLoadResult{T}.CanOverwrite"/> before saving over the key.
        /// </summary>
        UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads data, or returns <paramref name="defaultValue"/> when it could not be loaded for any reason.
        /// Corrupted data is backed up before returning. Prefer <see cref="TryLoadAsync{T}"/> when the caller saves
        /// back to the same key: it tells whether that is safe.
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
