using System;
using System.Collections.Generic;
using System.Linq;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Migrate;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using UnityEditor;
using ZLinq;

namespace _VuTH.Core.Persistant.SaveSystem.Editor
{
    /// <summary>
    /// Helper class to scan for available save system adapter implementations.
    /// Editor-only, used by SaveServiceSettingsTab.
    /// </summary>
    public static class SaveAdapterTypeScanner
    {
        private static readonly Dictionary<Type, List<Type>> CachedTypes = new();

        private static readonly Type[] TargetInterfaces = {
            typeof(IEncryptor),
            typeof(ISaveMigrator),
            typeof(ISerializer),
            typeof(ISaveBackend)
        };

        /// <summary>
        /// Get all concrete, non-abstract types implementing the given interface.
        /// </summary>
        public static List<Type> GetImplementations(Type interfaceType)
        {
            if (CachedTypes.TryGetValue(interfaceType, out var cached))
                return cached;

            // Use TypeCache for performance where possible
            var typeCacheViews = TypeCache.GetTypesDerivedFrom(interfaceType);
            var types = typeCacheViews.AsValueEnumerable()
                .Where(type => IsValidAdapterType(type, interfaceType)).ToList();

            // Sort by display name (namespace.type)
            types.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

            CachedTypes[interfaceType] = types;
            return types;
        }

        private static bool IsValidAdapterType(Type type, Type expectedInterface)
        {
            // Must be a class, not abstract, not generic definition
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                return false;

            // Must actually implement the expected interface
            if (!expectedInterface.IsAssignableFrom(type))
                return false;

            // Must have a public parameterless constructor for instantiation
            if (!HasPublicParameterlessCtor(type))
                return false;

            // Must have explicit [Serializable] attribute
            if (!HasSerializableAttribute(type))
                return false;

            // Exclude editor-only types by assembly name
            var assembly = type.Assembly;
            var assemblyName = assembly.GetName().Name;
            if (IsEditorAssembly(assemblyName))
                return false;

            // Exclude types with ObsoleteAttribute unless we want to show them (currently hiding)
            if (Attribute.IsDefined(type, typeof(ObsoleteAttribute)))
                return false;

            return true;
        }

        /// <summary>
        /// Check if a type has a public parameterless constructor.
        /// </summary>
        private static bool HasPublicParameterlessCtor(Type t)
        {
            return t.GetConstructor(Type.EmptyTypes) != null;
        }

        /// <summary>
        /// Check if a type has the explicit [Serializable] attribute.
        /// </summary>
        private static bool HasSerializableAttribute(Type t)
        {
            return Attribute.IsDefined(t, typeof(SerializableAttribute), inherit: false);
        }

        private static bool IsEditorAssembly(string assemblyName)
        {
            // List of known editor assembly name patterns to exclude
            return assemblyName.Contains("Editor") ||
                   assemblyName == "UnityEditor" ||
                   assemblyName.Contains("Assembly-CSharp-Editor") ||
                   assemblyName.Contains("VuTH.Boostrap.Editor") ||
                   assemblyName.Contains("VuTH.Common.Editor") ||
                   assemblyName.Contains("VuTH.Common.DI.Editor") ||
                   assemblyName.Contains("VuTH.Common.Log.Editor") ||
                   assemblyName.Contains("VuTH.MessagePipe.Editor") ||
                   assemblyName.StartsWith("Unity.*Editor", StringComparison.Ordinal);
        }

        /// <summary>
        /// Get a friendly display name for a type (e.g., "PlayerPrefsSaveBackend").
        /// </summary>
        public static string GetDisplayName(Type type)
        {
            // Simple: just the type name
            return type.Name;
        }

        /// <summary>
        /// Get the full name with namespace for disambiguation (e.g., "VuTH.Core.Persistant.SaveSystem.Backend.PlayerPrefsSaveBackend").
        /// </summary>
        public static string GetFullDisplayName(Type type)
        {
            return type.FullName ?? type.Name;
        }

        /// <summary>
        /// Get the assembly-qualified name for persistence/registration.
        /// </summary>
        public static string GetAssemblyQualifiedName(Type type)
        {
            return type.AssemblyQualifiedName;
        }

        /// <summary>
        /// Clear the cache (useful after assembly reload or domain refresh).
        /// </summary>
        public static void ClearCache()
        {
            CachedTypes.Clear();
        }

        /// <summary>
        /// Get all discovered types for a specific category as UI-friendly items.
        /// </summary>
        public static List<AdapterTypeItem> GetAdapterItems(Type interfaceType)
        {
            var types = GetImplementations(interfaceType);
            return types.Select(t => new AdapterTypeItem
            {
                Type = t,
                DisplayName = GetDisplayName(t),
                FullName = GetFullDisplayName(t),
                AssemblyQualifiedName = GetAssemblyQualifiedName(t),
                AssemblyName = t.Assembly.GetName().Name
            }).ToList();
        }
    }

    /// <summary>
    /// UI-friendly representation of an adapter type.
    /// </summary>
    public class AdapterTypeItem
    {
        public Type Type;
        public string DisplayName;
        public string FullName;
        public string AssemblyQualifiedName;
        public string AssemblyName;
    }
}
