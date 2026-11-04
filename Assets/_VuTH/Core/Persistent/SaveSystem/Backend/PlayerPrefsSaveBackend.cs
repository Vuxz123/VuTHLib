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
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Backend", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class PlayerPrefsSaveBackend : ISaveBackend, ISyncSaveBackend
    {
        /// <summary>Synchronous write for app pause / quit. Must be called on the main thread.</summary>
        public void SaveRaw(string key, string data)
        {
            PlayerPrefs.SetString(key, data);
            PlayerPrefs.Save();
        }

        public async UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            
            try
            {
                PlayerPrefs.SetString(key, data);
                PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerPrefsSaveBackend] Save failed for key '{key}': {ex.Message}");
                throw;
            }
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
            
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
