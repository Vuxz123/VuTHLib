using System.IO;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Handles persistence of MessagePipe lookup and whitelist assets.
    /// </summary>
    internal static class LookupPersistence
    {
        /// <summary>
        /// Load or create the MessagePipeAssemblyWhitelist asset.
        /// </summary>
        public static MessagePipeAssemblyWhitelist LoadOrCreateWhitelist()
        {
            var whitelist = AssetDatabase.LoadAssetAtPath<MessagePipeAssemblyWhitelist>(MessagePipeConstants.AbsoluteWhitelistPath);
            if (whitelist != null)
                return whitelist;

            // Ensure folder exists
            var folder = Path.GetDirectoryName(MessagePipeConstants.AbsoluteWhitelistPath);
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                PathUtilities.EnsureFolderExists(folder);
            }

            whitelist = ScriptableObject.CreateInstance<MessagePipeAssemblyWhitelist>();
            AssetDatabase.CreateAsset(whitelist, MessagePipeConstants.AbsoluteWhitelistPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MessagePipe Baker] Created whitelist asset at {MessagePipeConstants.AbsoluteWhitelistPath}");
            return whitelist;
        }

        /// <summary>
        /// Load or create the EventScopeLookup asset.
        /// </summary>
        public static EventScopeLookup LoadOrCreateLookup()
        {
            var lookup = AssetDatabase.LoadAssetAtPath<EventScopeLookup>(MessagePipeConstants.AbsoluteEventScopeLookupPath);
            if (lookup != null)
                return lookup;

            // Ensure folder exists
            var folder = Path.GetDirectoryName(MessagePipeConstants.AbsoluteEventScopeLookupPath);
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                PathUtilities.EnsureFolderExists(folder);
            }

            lookup = ScriptableObject.CreateInstance<EventScopeLookup>();
            AssetDatabase.CreateAsset(lookup, MessagePipeConstants.AbsoluteEventScopeLookupPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MessagePipe Baker] Created lookup asset at {MessagePipeConstants.AbsoluteEventScopeLookupPath}");
            return lookup;
        }

        /// <summary>
        /// Update the lookup asset with new entries and checksum.
        /// </summary>
        public static void UpdateLookup(EventScopeLookup lookup, System.Collections.Generic.List<EventScopeEntry> entries, string checksum)
        {
            lookup.SetData(entries, checksum);
            EditorUtility.SetDirty(lookup);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Load or create the MessagePipeOptionsConfig asset.
        /// </summary>
        public static MessagePipeOptionsConfig LoadOrCreateOptionsConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MessagePipeOptionsConfig>(MessagePipeConstants.AbsoluteOptionsConfigPath);
            if (config != null)
                return config;

            // Ensure folder exists
            var folder = Path.GetDirectoryName(MessagePipeConstants.AbsoluteOptionsConfigPath);
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                PathUtilities.EnsureFolderExists(folder);
            }

            config = ScriptableObject.CreateInstance<MessagePipeOptionsConfig>();
            AssetDatabase.CreateAsset(config, MessagePipeConstants.AbsoluteOptionsConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MessagePipe Baker] Created options config asset at {MessagePipeConstants.AbsoluteOptionsConfigPath}");
            return config;
        }

        /// <summary>
        /// Delete the lookup and whitelist assets, and the generated registrar file.
        /// Returns true if any files were deleted.
        /// </summary>
        public static bool ClearAllBakedData()
        {
            bool anyDeleted = false;

            // Delete lookup asset
            var lookupPath = MessagePipeConstants.AbsoluteEventScopeLookupPath;
            if (File.Exists(lookupPath))
            {
                try
                {
                    File.Delete(lookupPath);
                    Debug.Log($"[MessagePipe Baker] Deleted lookup asset: {lookupPath}");
                    anyDeleted = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MessagePipe Baker] Failed to delete lookup asset: {ex.Message}");
                }
            }

            // Delete whitelist asset
            var whitelistPath = MessagePipeConstants.AbsoluteWhitelistPath;
            if (File.Exists(whitelistPath))
            {
                try
                {
                    File.Delete(whitelistPath);
                    Debug.Log($"[MessagePipe Baker] Deleted whitelist asset: {whitelistPath}");
                    anyDeleted = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MessagePipe Baker] Failed to delete whitelist asset: {ex.Message}");
                }
            }

            // Delete registrar file
            var registrarPath = Path.Combine(RegistrarGenerator.RegistrarFolder, RegistrarGenerator.RegistrarFileName);
            if (File.Exists(registrarPath))
            {
                try
                {
                    File.Delete(registrarPath);
                    Debug.Log($"[MessagePipe Baker] Deleted registrar file: {registrarPath}");
                    anyDeleted = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MessagePipe Baker] Failed to delete registrar file: {ex.Message}");
                }
            }

            if (anyDeleted)
            {
                AssetDatabase.Refresh();
            }

            return anyDeleted;
        }
    }
}
