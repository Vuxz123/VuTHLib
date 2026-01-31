using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Common.MessagePipe.Configuration
{
    /// <summary>
    /// Whitelist of assembly names to scan for MessagePipe event attributes.
    /// Add or remove entries as needed. Only assemblies in this list will be scanned.
    /// Assemblies can also opt-in by adding [VuTHMessagePipeEventAssembly] attribute.
    /// </summary>
    [CreateAssetMenu(fileName = "MessagePipeAssemblyWhitelist", menuName = "VuTH/MessagePipe/Assembly Whitelist")]
    public class MessagePipeAssemblyWhitelist : ScriptableObject
    {
        [Header("Assembly Names to Scan")]
        [Tooltip("List of assembly names (without .dll extension) to scan for MessagePipe events")]
        [SerializeField] private List<string> assemblyNames = new()
        {
            "VuTH.Gameplay",
            "VuTH.Core",
            "VuTH.Common"
        };

        public IReadOnlyList<string> AssemblyNames => assemblyNames;

        // Cached HashSet for O(1) lookup with case-insensitive comparison
        private HashSet<string> _cache;

        /// <summary>
        /// Check if an assembly name is in the whitelist.
        /// Uses HashSet cache for O(1) lookup with case-insensitive comparison.
        /// </summary>
        public bool Contains(string assemblyName)
        {
            _cache ??= new HashSet<string>(assemblyNames, System.StringComparer.OrdinalIgnoreCase);
            return _cache.Contains(assemblyName);
        }

        private void OnEnable()
        {
            // Clear cache on enable to ensure fresh data after domain reload
            _cache = null;
        }
    }
}
