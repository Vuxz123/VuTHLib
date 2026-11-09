using UnityEngine.Scripting.APIUpdating;
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Core.Persistent.SaveSystem.Backend
{
    /// <summary>
    /// Backend storing one JSON file per key in <see cref="Application.persistentDataPath"/> (or a custom folder).
    /// Writes go to a temporary file that is then swapped in, so a crash mid-write never leaves a truncated save.
    /// <para>
    /// Each write takes a ticket when it starts. A write that finishes after a newer one was already swapped in is
    /// dropped: an async save started before an app-pause flush can no longer replace the flushed (newer) data.
    /// </para>
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_VuTH.Core.Persistant.SaveSystem.Backend", sourceAssembly: "VuTH.Persistant.SaveSystem")]
    public class JsonFileSaveBackend : IOrderedSaveBackend, ISyncSaveBackend
    {
        private const string Extension = ".json";

        private readonly string? _basePath = null;

        private string BasePath => _basePath ?? Application.persistentDataPath;

        // Created on first use: an instance deserialized from a profile ([SerializeReference]) may skip initializers.
        [NonSerialized] private Dictionary<string, long>? _committedTickets;
        [NonSerialized] private long _lastTicket;

        public long ReserveWrite() => Interlocked.Increment(ref _lastTicket);

        /// <summary>Swaps the temp file in unless a newer write of the same file already was; true if swapped.</summary>
        private bool Commit(string tempPath, string filePath, long ticket)
        {
            lock (this)
            {
                _committedTickets ??= new Dictionary<string, long>();
                if (_committedTickets.TryGetValue(filePath, out var committed) && committed > ticket)
                {
                    TryDelete(tempPath);
                    return false;
                }

                SwapIn(tempPath, filePath);
                _committedTickets[filePath] = ticket;
                return true;
            }
        }

        public JsonFileSaveBackend()
        {
        }

        public JsonFileSaveBackend(string basePath)
        {
            _basePath = basePath;
        }

        #region Paths

        /// <summary>
        /// Versioned SHA-256 name for every key, with no literal-key names in the new namespace. Lowercase hex
        /// preserves case-sensitive key identity on Windows and bounds filename length for long Unicode keys.
        /// </summary>
        internal string GetFilePath(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            // Encode code units directly so even unpaired surrogates remain distinct (UTF8 replacement would not).
            var bytes = new byte[key.Length * 2];
            for (var i = 0; i < key.Length; i++)
            {
                bytes[i * 2] = (byte)key[i];
                bytes[i * 2 + 1] = (byte)(key[i] >> 8);
            }
            using var hash = SHA256.Create();
            var encoded = new StringBuilder(64);
            foreach (var b in hash.ComputeHash(bytes)) encoded.Append(b.ToString("x2"));
            return Path.Combine(BasePath, ".vuth-keys-v2", encoded + Extension);
        }

        // Historical names are read-only: their key identity was ambiguous, so never modify them.
        private string GetPreviousFilePath(string key)
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
            if (File.Exists(path + ".deleted")) return null;

            var previous = GetPreviousFilePath(key);
            if (File.Exists(previous)) return previous;
            if (File.Exists(previous + ".deleted")) return null;

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
            var ticket = ReserveWrite();
            var filePath = GetFilePath(key);
            var tempPath = PrepareTempPath(filePath);
            try
            {
                WriteDurably(tempPath, data);
                Commit(tempPath, filePath, ticket);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        public UniTask SaveRawAsync(string key, string data, CancellationToken cancellationToken = default) =>
            SaveRawOrderedAsync(key, data, ReserveWrite(), cancellationToken);

        public async UniTask SaveRawOrderedAsync(string key, string data, long ticket, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = GetFilePath(key);
            var tempPath = PrepareTempPath(filePath);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           4096, useAsync: true))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                    stream.Flush(flushToDisk: true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                Commit(tempPath, filePath, ticket);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        private static void WriteDurably(string path, string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
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

            // Fail safely on platforms without atomic replacement; never truncate the live save as a fallback.
            File.Replace(tempPath, filePath, destinationBackupFileName: null);
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
            lock (this)
            {
                // Persist before deleting the current file. A later save wins over the marker, but deleting that
                // save must still block legacy fallback after a restart. Never remove the marker on writes.
                if (!File.Exists(filePath + ".deleted"))
                {
                    var marker = filePath + ".deleted";
                    var temp = PrepareTempPath(marker);
                    try
                    {
                        WriteDurably(temp, string.Empty);
                        SwapIn(temp, marker);
                    }
                    finally { TryDelete(temp); }
                }
                if (File.Exists(filePath)) File.Delete(filePath);
                _committedTickets ??= new Dictionary<string, long>();
                _committedTickets[filePath] = ReserveWrite();
            }

            return UniTask.CompletedTask;
        }

        #endregion
    }
}
