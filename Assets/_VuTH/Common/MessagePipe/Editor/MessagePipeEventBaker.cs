using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Editor tool to bake MessagePipe event types into EventScopeLookup asset.
    /// Scans only whitelisted assemblies for types with [MessagePipeEvent] attribute.
    /// Also generates MessagePipeRegistrar.cs for optimized runtime registration.
    /// </summary>
    public static class MessagePipeEventBaker
    {
        private const string MenuPath = "VuTH/MessagePipe/Bake Event Scope Lookup";
        private const string ValidateMenuPath = "VuTH/MessagePipe/Validate Bake (Check Stale)";
        private const string ClearMenuPath = "VuTH/MessagePipe/Clear Baked";

        [MenuItem(MenuPath, priority = 100)]
        public static void BakeEventScopeLookup()
        {
            var whitelist = LookupPersistence.LoadOrCreateWhitelist();
            if (whitelist == null)
            {
                Debug.LogError("[MessagePipe Baker] Failed to load whitelist asset.");
                return;
            }

            var entries = EventScanner.ScanAssembliesForEvents(whitelist);
            var checksum = ChecksumCalculator.ComputeChecksum(entries);

            var lookup = LookupPersistence.LoadOrCreateLookup();
            if (lookup == null)
            {
                Debug.LogError("[MessagePipe Baker] Failed to create lookup asset.");
                return;
            }

            LookupPersistence.UpdateLookup(lookup, entries, checksum);

            // Load options config to check preserveRegistrar flag
            var optionsConfig = LookupPersistence.LoadOrCreateOptionsConfig();
            var preserveRegistrar = optionsConfig != null && optionsConfig.preserveRegistrar;

            // Generate MessagePipeRegistrar for optimized runtime registration
            RegistrarGenerator.GenerateRegistrar(entries, preserveRegistrar);

            Debug.Log($"[MessagePipe Baker] Baked {entries.Count} event(s) to {MessagePipeConstants.AbsoluteEventScopeLookupPath}");
            ChecksumCalculator.LogEntrySummary(entries);
        }

        [MenuItem(ValidateMenuPath, priority = 101)]
        public static void ValidateBake()
        {
            var whitelist = LookupPersistence.LoadOrCreateWhitelist();
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

            var entries = EventScanner.ScanAssembliesForEvents(whitelist);
            var currentChecksum = ChecksumCalculator.ComputeChecksum(entries);

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

        [MenuItem(ClearMenuPath, priority = 102)]
        public static void ClearBaked()
        {
            bool anyDeleted = LookupPersistence.ClearAllBakedData();
            Debug.Log(anyDeleted
                ? "[MessagePipe Baker] Cleared all baked data. Run 'Bake Event Scope Lookup' to regenerate."
                : "[MessagePipe Baker] No baked data found to clear.");
        }
    }
}
