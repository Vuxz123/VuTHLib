using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Core;
using Cysharp.Text;
using UnityEngine;
using ZLinq;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Computes MD5 checksums for event entries to detect stale bakes.
    /// </summary>
    internal static class ChecksumCalculator
    {
        /// <summary>
        /// Compute MD5 checksum of event entries for change detection.
        /// </summary>
        public static string ComputeChecksum(List<EventScopeEntry> entries)
        {
            using var sb = ZString.CreateStringBuilder();
            foreach (var entry in entries)
            {
                sb.Append(entry.typeFullName);
                sb.Append(':');
                sb.Append((int)entry.scope);
                sb.Append(':');
                sb.Append(entry.sceneName ?? string.Empty);
                sb.Append(';');
            }

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Log a summary of the entries (global vs scene counts and details).
        /// </summary>
        public static void LogEntrySummary(List<EventScopeEntry> entries)
        {
            var globalCount = entries.AsValueEnumerable().Count(e => e.scope == EventScope.Global);
            var sceneCount = entries.AsValueEnumerable().Count(e => e.scope == EventScope.Scene);
            Debug.Log($"[MessagePipe Baker] Summary: {globalCount} Global, {sceneCount} Scene");

            foreach (var entry in entries)
            {
                var sceneInfo = entry.scope == EventScope.Scene ? $": {entry.sceneName}" : "";
                Debug.Log($"  [{entry.scope}{sceneInfo}] {entry.typeFullName}");
            }
        }
    }
}
