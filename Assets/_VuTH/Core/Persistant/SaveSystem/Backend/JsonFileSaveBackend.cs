#nullable enable
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZLinq;

namespace _VuTH.Core.Persistant.SaveSystem.Backend
{
    /// <summary>
    /// Backend using JSON files in Application.persistentDataPath.
    /// Suitable for larger data or when file system access is needed.
    /// </summary>
    public class JsonFileSaveBackend : ISaveBackend
    {
        private readonly string _basePath;

        public JsonFileSaveBackend()
        {
            _basePath = Application.persistentDataPath;
        }

        public JsonFileSaveBackend(string basePath)
        {
            _basePath = basePath;
        }

        private string GetFilePath(string key)
        {
            // Sanitize key to be a valid filename
            key = Path.GetInvalidFileNameChars()
                .AsValueEnumerable()
                .Aggregate(key, (current, invalidFileNameChar) => 
                    current.Replace(invalidFileNameChar, '_'));
            return Path.Combine(_basePath, $"{key}.json");
        }

        public async UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken)
        {
            string filePath = GetFilePath(key);
            
            try
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(filePath, data, cancellationToken);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[JsonFileSaveBackend] Save failed for key '{key}': {ex.Message}");
                throw;
            }
        }

        public async UniTask<string?> LoadRawAsync(
            string key, CancellationToken cancellationToken)
        {
            var filePath = GetFilePath(key);
            
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                return await File.ReadAllTextAsync(filePath, cancellationToken);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[JsonFileSaveBackend] Load failed for key '{key}': {ex.Message}");
                return null;
            }
        }

        public async UniTask<bool> Exists(string key, CancellationToken cancellationToken)
        {
            var filePath = GetFilePath(key);
            return File.Exists(filePath);
        }

        public async UniTask DeleteAsync(string key, CancellationToken cancellationToken)
        {
            string filePath = GetFilePath(key);
            
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[JsonFileSaveBackend] Delete failed for key '{key}': {ex.Message}");
                throw;
            }
        }
    }
}
