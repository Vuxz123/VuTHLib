using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _VuTH.Common.MessagePipe.Configuration;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Non-UI helpers for editing a <see cref="MessagePipeAssemblyWhitelist"/>: name normalization,
    /// case-insensitive duplicate detection and access to the serialized (private) name list.
    /// </summary>
    public static class AssemblyWhitelistEditing
    {
        private const string AssemblyNamesFieldName = "assemblyNames";

        /// <summary>Trims the user-entered assembly name. Returns <c>null</c> for <c>null</c> input.</summary>
        public static string NormalizeName(string rawName) => rawName?.Trim();

        /// <summary>Returns true if <paramref name="assemblyName"/> already exists in <paramref name="assemblyNames"/> (case-insensitive).</summary>
        public static bool ContainsName(IEnumerable<string> assemblyNames, string assemblyName)
        {
            return assemblyNames.Any(a => a.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reflection accessor for the private serialized <c>assemblyNames</c> field (there is no public setter).
        /// Returns <c>null</c> if the field cannot be found.
        /// </summary>
        public static FieldInfo GetAssemblyNamesField()
        {
            return typeof(MessagePipeAssemblyWhitelist).GetField(AssemblyNamesFieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}
