#nullable enable
using System;
using System.IO;
using System.Threading;
using _VuTH.Common.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZLinq;

namespace _VuTH.Core.Persistant.SaveSystem.Backend
{
    /// <summary>
    /// Backend using JSON files in Application.persistentDataPath.
    /// Suitable for larger data or when file system access is needed.
    /// </summary>
    [Serializable]
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
            var filePath = Path.Combine(_basePath, $"{key}.json");
            this.Log($"GetFilePath key='{key}' basePath='{_basePath}' result='{filePath}'");
            return filePath;
        }

        public async UniTask SaveRawAsync(string key, string? data, CancellationToken cancellationToken)
        {
            string filePath = GetFilePath(key);
            this.Log($"SaveRawAsync key='{key}' filePath='{filePath}' dataLength={data?.Length ?? 0}");
            
            try
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    this.Log($"Creating directory: {directory}");
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(filePath, data, cancellationToken);
                this.Log($"SaveRawAsync SUCCESS for key '{key}'");
            }
            catch (Exception ex)
            {
                this.LogError($"Save failed for key '{key}': {ex.Message}");
                throw;
            }
        }

        public async UniTask<string?> LoadRawAsync(
            string key, CancellationToken cancellationToken)
        {
            var filePath = GetFilePath(key);
            this.Log($"LoadRawAsync key='{key}' filePath='{filePath}'");
            
            try
            {
                if (!File.Exists(filePath))
                {
                    this.Log($"LoadRawAsync: File does not exist: {filePath}");
                    return null;
                }

                var result = await File.ReadAllTextAsync(filePath, cancellationToken);
                this.Log($"LoadRawAsync SUCCESS for key '{key}' length={result?.Length ?? 0}");
                return result;
            }
            catch (Exception ex)
            {
                this.LogError($"Load failed for key '{key}': {ex.Message}");
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
            var filePath = GetFilePath(key);
            
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                this.LogError($"Delete failed for key '{key}': {ex.Message}");
                throw;
            }
        }
    }
}
