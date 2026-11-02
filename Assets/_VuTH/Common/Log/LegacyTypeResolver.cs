using System;
using System.Collections.Generic;

namespace _VuTH.Common
{
    /// <summary>
    /// Resolves type-name strings that were serialized (e.g. AssemblyQualifiedName in ScriptableObject assets)
    /// before the library renamed its assemblies / namespaces:
    /// <list type="bullet">
    /// <item>Persistant  → Persistent  (assemblies VuTH.Persistant.* and namespace _VuTH.Core.Persistant.*)</item>
    /// <item>Booststrap / Boostrap → Bootstrap</item>
    /// <item>_VuTH.Core.GameCycle.Screen.Core.A → _VuTH.Core.GameCycle.Screen.Core</item>
    /// <item>VuTH.Common.MessagePipe → _VuTH.Common.MessagePipe</item>
    /// </list>
    /// Lives in VuTH.Common.Log because that is the lowest-level assembly every module already references.
    /// </summary>
    public static class LegacyTypeResolver
    {
        private static readonly (string Old, string New)[] Renames =
        {
            ("VuTH..Persistant", "VuTH.Persistent"),
            ("Persistant", "Persistent"),
            ("Booststrap", "Bootstrap"),
            ("Boostrap", "Bootstrap"),
            (".Screen.Core.A.", ".Screen.Core."),
        };

        private static readonly Dictionary<string, Type> Cache = new(StringComparer.Ordinal);

        /// <summary>
        /// Like <see cref="Type.GetType(string, bool)"/>, but falls back to the post-rename name and finally to
        /// scanning loaded assemblies by full name. Returns null if nothing matches.
        /// </summary>
        public static Type Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            lock (Cache)
            {
                if (Cache.TryGetValue(typeName, out var cached)) return cached;
            }

            var type = Type.GetType(typeName, throwOnError: false)
                       ?? TryMigrated(typeName)
                       ?? ScanAssemblies(typeName);

            lock (Cache)
            {
                Cache[typeName] = type;
            }

            return type;
        }

        /// <summary>Applies the rename table to a serialized type name. Returns the input when nothing changed.</summary>
        public static string Migrate(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;

            var result = typeName;
            foreach (var (oldName, newName) in Renames)
            {
                result = result.Replace(oldName, newName);
            }

            // "VuTH.Common.MessagePipe" without the leading underscore (namespace only — the assembly is also
            // called VuTH.Common.MessagePipe, so only rewrite the part before the first comma).
            var comma = result.IndexOf(',');
            var typePart = comma < 0 ? result : result.Substring(0, comma);
            if (typePart.StartsWith("VuTH.Common.MessagePipe.", StringComparison.Ordinal))
            {
                result = "_" + result;
            }

            return result;
        }

        private static Type TryMigrated(string typeName)
        {
            var migrated = Migrate(typeName);
            return ReferenceEquals(migrated, typeName) || migrated == typeName
                ? null
                : Type.GetType(migrated, throwOnError: false) ?? ScanAssemblies(migrated);
        }

        private static Type ScanAssemblies(string typeName)
        {
            // Strip assembly qualification: "Ns.Type, Assembly, Version=..." → "Ns.Type"
            var comma = typeName.IndexOf(',');
            var fullName = comma < 0 ? typeName : typeName.Substring(0, comma).Trim();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, throwOnError: false);
                if (type != null) return type;
            }

            return null;
        }
    }
}
