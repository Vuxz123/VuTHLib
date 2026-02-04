#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Core.Persistant.SaveSystem.Backend
{
    /// <summary>
    /// Backend using Unity's PlayerPrefs.
    /// Suitable for simple local storage needs.
    /// </summary>
    [Serializable]
    public class PlayerPrefsSaveBackend : ISaveBackend
    {
        public async UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken)
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

        public async UniTask<string?> LoadRawAsync(string key, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            
            if (!PlayerPrefs.HasKey(key))
            {
                return null;
            }

            return PlayerPrefs.GetString(key);
        }

        public async UniTask<bool> Exists(string key, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            return PlayerPrefs.HasKey(key);
        }

        public async UniTask DeleteAsync(string key, CancellationToken cancellationToken)
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
