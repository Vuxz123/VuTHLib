using UnityEngine.Scripting.APIUpdating;
#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Core.Persistent.SaveSystem.Backend
{
    /// <summary>
    /// Backend using Unity's PlayerPrefs. The default backend (it is what shipped builds used so far).
    /// Suited to small saves only: PlayerPrefs live in the registry on Windows and are capped at about 1 MB on WebGL.
    /// Use <see cref="JsonFileSaveBackend"/> for larger data.
    /// <para>
    /// <c>PlayerPrefs.Save()</c> rewrites every key on disk, so async writes made in the same frame share one flush at
    /// the end of the frame (each call still completes only once its data is on disk). Synchronous writes, used on app
    /// pause / quit, flush immediately.
    /// </para>
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Backend", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class PlayerPrefsSaveBackend : ISaveBackend, ISyncSaveBackend
    {
        [NonSerialized] private UniTaskCompletionSource? _pendingFlush;

        /// <summary>Synchronous write for app pause / quit. Must be called on the main thread.</summary>
        public void SaveRaw(string key, string data)
        {
            PlayerPrefs.SetString(key, data);
            PlayerPrefs.Save();
        }

        public async UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            PlayerPrefs.SetString(key, data);
            await FlushAsync();
        }

        public async UniTask<string?> LoadRawAsync(string key, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);

            if (!PlayerPrefs.HasKey(key))
            {
                return null;
            }

            return PlayerPrefs.GetString(key);
        }

        public async UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            return PlayerPrefs.HasKey(key);
        }

        public async UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);

            if (!PlayerPrefs.HasKey(key)) return;

            PlayerPrefs.DeleteKey(key);
            await FlushAsync();
        }

        /// <summary>Joins the flush scheduled for the end of this frame, scheduling it if needed.</summary>
        private UniTask FlushAsync()
        {
            if (_pendingFlush == null)
            {
                _pendingFlush = new UniTaskCompletionSource();
                FlushAtEndOfFrameAsync(_pendingFlush).Forget();
            }

            return _pendingFlush.Task;
        }

        private async UniTaskVoid FlushAtEndOfFrameAsync(UniTaskCompletionSource flush)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            // Writes made from now on need the next flush.
            _pendingFlush = null;
            try
            {
                PlayerPrefs.Save();
                flush.TrySetResult();
            }
            catch (Exception ex)
            {
                flush.TrySetException(ex);
            }
        }
    }
}
