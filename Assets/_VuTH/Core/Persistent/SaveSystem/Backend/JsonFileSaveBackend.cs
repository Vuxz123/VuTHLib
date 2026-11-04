using UnityEngine.Scripting.APIUpdating;
#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Core.Persistent.SaveSystem.Backend
{
    /// <summary>
    /// Backend storing one JSON file per key in <see cref="Application.persistentDataPath"/> (or a custom folder).
    /// Writes go to a temporary file that is then swapped in, so a crash mid-write never leaves a truncated save.
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Backend", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class JsonFileSaveBackend : ISaveBackend, ISyncSaveBackend
    {
        private const string Extension = ".json";

        private readonly string? _basePath = null;

        private string BasePath => _basePath ?? Application.persistentDataPath;

        public JsonFileSaveBackend()
        {
        }

        public JsonFileSaveBackend(string basePath)
        {
            _basePath = basePath;
        }

        #region Paths

        /// <summary>
        /// File of a key. Keys with characters that are invalid in file names get a hash suffix, so two keys that
        /// sanitize to the same name ("a/b" and "a_b") never share a file.
        /// </summary>
        internal string GetFilePath(string key)
        {
            var sanitized = Sanitize(key);
            var fileName = sanitized == key ? key : $"{sanitized}-{StableHash(key):x8}";
            return Path.Combine(BasePath, fileName + Extension);
        }

        /// <summary>
        /// Pre-hash file name of a key with invalid characters: read when the key has no file of its own yet, so saves
        /// written by older versions still load. Never written or deleted.
        /// </summary>
        private string? GetLegacyFilePath(string key)
        {
            var sanitized = Sanitize(key);
            return sanitized == key ? null : Path.Combine(BasePath, sanitized + Extension);
        }

        private string? FindExistingFile(string key)
        {
            var path = GetFilePath(key);
            if (File.Exists(path)) return path;

            var legacy = GetLegacyFilePath(key);
            return legacy != null && File.Exists(legacy) ? legacy : null;
        }

        private static string Sanitize(string key)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(key.Length);
            foreach (var c in key)
            {
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            return builder.ToString();
        }

        /// <summary>FNV-1a: stable across runs and platforms, unlike <see cref="string.GetHashCode()"/>.</summary>
        private static uint StableHash(string value)
        {
            var hash = 2166136261u;
            foreach (var c in value)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }

        #endregion

        #region Write

        /// <summary>Synchronous write used for app pause / quit flushes.</summary>
        public void SaveRaw(string key, string data)
        {
            var filePath = GetFilePath(key);
            var tempPath = PrepareTempPath(filePath);
            try
            {
                File.WriteAllText(tempPath, data);
                SwapIn(tempPath, filePath);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        public async UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken = default)
        {
            var filePath = GetFilePath(key);
            var tempPath = PrepareTempPath(filePath);
            try
            {
                await File.WriteAllTextAsync(tempPath, data, cancellationToken);
                SwapIn(tempPath, filePath);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        /// <summary>Unique per write: a sync flush and an in-flight async save never share a temp file.</summary>
        private static string PrepareTempPath(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            return $"{filePath}.{Guid.NewGuid():N}.tmp";
        }

        private static void SwapIn(string tempPath, string filePath)
        {
            if (!File.Exists(filePath))
            {
                File.Move(tempPath, filePath);
                return;
            }

            try
            {
                // Atomic rename on Windows / Unix (Android, iOS, macOS).
                File.Replace(tempPath, filePath, destinationBackupFileName: null);
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or IOException)
            {
                File.Copy(tempPath, filePath, overwrite: true);
                File.Delete(tempPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best effort: a stray temp file is harmless.
            }
        }

        #endregion

        #region Read / delete

        public async UniTask<string?> LoadRawAsync(string key, CancellationToken cancellationToken = default)
        {
            var filePath = FindExistingFile(key);
            if (filePath == null) return null;

            try
            {
                return await File.ReadAllTextAsync(filePath, cancellationToken);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return null; // Deleted between the check and the read.
            }
        }

        public UniTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(FindExistingFile(key) != null);
        }

        public UniTask DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The legacy file is not deleted: its name is also the file of the key it sanitized to ("a/b" → "a_b"),
            // which may hold another key's data.
            var filePath = GetFilePath(key);
            if (File.Exists(filePath)) File.Delete(filePath);

            return UniTask.CompletedTask;
        }

        #endregion
    }
}
