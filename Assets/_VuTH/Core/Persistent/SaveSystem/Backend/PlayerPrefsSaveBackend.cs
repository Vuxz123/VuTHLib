using UnityEngine.Scripting.APIUpdating;
#nullable enable
using System;
using System.Collections.Generic;
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
    public class PlayerPrefsSaveBackend : IOrderedSaveBackend, ISyncSaveBackend
    {
        [NonSerialized] private UniTaskCompletionSource? _pendingFlush;
        [NonSerialized] private long _lastTicket;
        [NonSerialized] private Dictionary<string, long>? _writtenTickets;

        public long ReserveWrite() => Interlocked.Increment(ref _lastTicket);

        // Accessed only on Unity's main thread, like PlayerPrefs itself.
        private bool Write(string key, string data, long ticket)
        {
            _writtenTickets ??= new Dictionary<string, long>();
            if (_writtenTickets.TryGetValue(key, out var written) && written > ticket) return false;
            PlayerPrefs.SetString(key, data);
            _writtenTickets[key] = ticket;
            return true;
        }

        /// <summary>Synchronous write for app pause / quit. Must be called on the main thread.</summary>
        public void SaveRaw(string key, string data)
        {
            Write(key, data, ReserveWrite());
            PlayerPrefs.Save();
        }

        public UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken = default) =>
            SaveRawOrderedAsync(key, data, ReserveWrite(), cancellationToken);

        public async UniTask SaveRawOrderedAsync(string key, string data, long ticket, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Write(key, data, ticket);
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

            _writtenTickets ??= new Dictionary<string, long>();
            _writtenTickets[key] = ReserveWrite();
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
