#nullable enable
using System.Threading;
using _VuTH.Core.Persistent.SaveSystem.Migrate;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// Optional capability: save and load data with its own <see cref="SaveSchema"/> (version and migrators) instead of
    /// the service-wide one. Persistence packages that declare a schema use it, so changing the format of one package
    /// needs no migrator for the others. <see cref="SaveService"/> and <see cref="SaveServiceManager"/> implement it.
    /// </summary>
    public interface IVersionedSaveService
    {
        /// <summary>Like <see cref="ISaveService.SaveAsync{T}"/>, writing <paramref name="schema"/>'s version.</summary>
        UniTask SaveAsync<T>(string key, T data, SaveSchema schema, CancellationToken cancellationToken = default);

        /// <summary>
        /// Like <see cref="ISaveService.TryLoadAsync{T}"/>, migrating older saves with <paramref name="schema"/>'s
        /// migrators and refusing saves newer than its version.
        /// </summary>
        UniTask<SaveLoadResult<T>> TryLoadAsync<T>(string key, SaveSchema schema, CancellationToken cancellationToken = default);

        /// <summary>Like <see cref="ISyncSaveService.TrySaveImmediate{T}"/>, writing <paramref name="schema"/>'s version.</summary>
        bool TrySaveImmediate<T>(string key, T data, SaveSchema schema);
    }
}
