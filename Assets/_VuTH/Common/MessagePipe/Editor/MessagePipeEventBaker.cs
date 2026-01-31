using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using UnityEditor;
using UnityEngine;

namespace VuTH.Common.MessagePipe.Editor._VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Editor tool to bake MessagePipe event types into EventScopeLookup asset.
    /// Scans only whitelisted assemblies for types with [MessagePipeEvent] attribute.
    /// </summary>
    public static class MessagePipeEventBaker
    {
        private const string MenuPath = "VuTH/MessagePipe/Bake Event Scope Lookup";
        private const string ValidateMenuPath = "VuTH/MessagePipe/Validate Bake (Check Stale)";

        [MenuItem(MenuPath, priority = 100)]
        public static void BakeEventScopeLookup()
        {
            var whitelist = LoadOrCreateWhitelist();
            if (whitelist == null)
            {
                Debug.LogError("[MessagePipe Baker] Failed to load whitelist asset.");
                return;
            }

            var entries = ScanAssembliesForEvents(whitelist);
            var checksum = ComputeChecksum(entries);

            var lookup = LoadOrCreateLookup();
            if (lookup == null)
            {
                Debug.LogError("[MessagePipe Baker] Failed to create lookup asset.");
                return;
            }

            lookup.SetData(entries, checksum);
            EditorUtility.SetDirty(lookup);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MessagePipe Baker] Baked {entries.Count} event(s) to {MessagePipeConstants.AbsoluteEventScopeLookupPath}");
            LogEntrySummary(entries);
        }

        [MenuItem(ValidateMenuPath, priority = 101)]
        public static void ValidateBake()
        {
            var whitelist = LoadOrCreateWhitelist();
            if (whitelist == null)
            {
                Debug.LogError("[MessagePipe Baker] Failed to load whitelist asset.");
                return;
            }

            var lookup = AssetDatabase.LoadAssetAtPath<EventScopeLookup>(MessagePipeConstants.AbsoluteEventScopeLookupPath);
            if (lookup == null)
            {
                Debug.LogWarning("[MessagePipe Baker] No lookup asset found. Run Bake first.");
                return;
            }

            var entries = ScanAssembliesForEvents(whitelist);
            var currentChecksum = ComputeChecksum(entries);

            if (lookup.Checksum == currentChecksum)
            {
                Debug.Log($"[MessagePipe Baker] Lookup is up-to-date. Version: {lookup.Version}, Baked: {lookup.BakedAt}");
            }
            else
            {
                Debug.LogWarning($"[MessagePipe Baker] Lookup is STALE! Current checksum differs. Please re-bake.\n" +
                                 $"Asset checksum: {lookup.Checksum}\n" +
                                 $"Current checksum: {currentChecksum}");
            }
        }

        private static List<EventScopeEntry> ScanAssembliesForEvents(MessagePipeAssemblyWhitelist whitelist)
        {
            var entries = new List<EventScopeEntry>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                if (!IsWhitelisted(assembly, whitelist))
                    continue;

                var assemblyName = assembly.GetName().Name;
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        // Validate type is concrete (not abstract or generic)
                        if ((!type.IsValueType && !type.IsClass) || type.IsAbstract || type.ContainsGenericParameters)
                            continue;

                        var attr = type.GetCustomAttribute<MessagePipeEventAttribute>();
                        var scope = attr?.Scope ?? EventScope.Global;

                        entries.Add(new EventScopeEntry
                        {
                            typeFullName = type.AssemblyQualifiedName,
                            scope = scope
                        });
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"[MessagePipe Baker] Failed to load some types from {assemblyName}: {ex.Message}");
                    // Process loadable types
                    foreach (var type in ex.Types.Where(t => t != null))
                    {
                        // Validate type is concrete (not abstract or generic)
                        if ((!type.IsValueType && !type.IsClass) || type.IsAbstract || type.ContainsGenericParameters)
                            continue;

                        var attr = type.GetCustomAttribute<MessagePipeEventAttribute>();
                        var scope = attr?.Scope ?? EventScope.Global;

                        entries.Add(new EventScopeEntry
                        {
                            typeFullName = type.AssemblyQualifiedName,
                            scope = scope
                        });
                    }
                }
            }

            // Sort for deterministic output
            entries.Sort((a, b) => string.Compare(a.typeFullName, b.typeFullName, StringComparison.Ordinal));
            return entries;
        }

        private static string ComputeChecksum(List<EventScopeEntry> entries)
        {
            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.Append(entry.typeFullName);
                sb.Append(':');
                sb.Append((int)entry.scope);
                sb.Append(';');
            }

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static void LogEntrySummary(List<EventScopeEntry> entries)
        {
            var globalCount = entries.Count(e => e.scope == EventScope.Global);
            var sceneCount = entries.Count(e => e.scope == EventScope.Scene);
            Debug.Log($"[MessagePipe Baker] Summary: {globalCount} Global, {sceneCount} Scene");

            foreach (var entry in entries)
            {
                Debug.Log($"  [{entry.scope}] {entry.typeFullName}");
            }
        }

        private static MessagePipeAssemblyWhitelist LoadOrCreateWhitelist()
        {
            var whitelist = AssetDatabase.LoadAssetAtPath<MessagePipeAssemblyWhitelist>(MessagePipeConstants.AbsoluteWhitelistPath);
            if (whitelist != null)
                return whitelist;

            // Ensure folder exists
            var folder = Path.GetDirectoryName(MessagePipeConstants.AbsoluteWhitelistPath);
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                EnsureFolderExists(folder);
            }

            whitelist = ScriptableObject.CreateInstance<MessagePipeAssemblyWhitelist>();
            AssetDatabase.CreateAsset(whitelist, MessagePipeConstants.AbsoluteWhitelistPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MessagePipe Baker] Created whitelist asset at {MessagePipeConstants.AbsoluteWhitelistPath}");
            return whitelist;
        }

        private static EventScopeLookup LoadOrCreateLookup()
        {
            var lookup = AssetDatabase.LoadAssetAtPath<EventScopeLookup>(MessagePipeConstants.AbsoluteEventScopeLookupPath);
            if (lookup != null)
                return lookup;

            // Ensure folder exists
            var folder = Path.GetDirectoryName(MessagePipeConstants.AbsoluteEventScopeLookupPath);
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                EnsureFolderExists(folder);
            }

            lookup = ScriptableObject.CreateInstance<EventScopeLookup>();
            AssetDatabase.CreateAsset(lookup, MessagePipeConstants.AbsoluteEventScopeLookupPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MessagePipe Baker] Created lookup asset at {MessagePipeConstants.AbsoluteEventScopeLookupPath}");
            return lookup;
        }

        /// <summary>
        /// Check if an assembly is whitelisted via whitelist asset or marker attribute.
        /// </summary>
        private static bool IsWhitelisted(Assembly assembly, MessagePipeAssemblyWhitelist whitelist)
        {
            var assemblyName = assembly.GetName().Name;
            if (whitelist.Contains(assemblyName))
                return true;

            return assembly.GetCustomAttribute<VuTHMessagePipeEventAssemblyAttribute>() != null;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            var parts = folderPath.Replace("\\", "/").Split('/');
            var currentPath = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                var nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = nextPath;
            }
        }
    }
}
