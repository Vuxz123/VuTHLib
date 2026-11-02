using _VuTH.Common;
using System;
using System.Linq;
using _VuTH.Common.Log;

namespace _VuTH.Core.Persistent.DataPackage
{
    public static class PersistencePackageFactory
    {
        public static bool TryCreate(string typeName, out IPersistencePackage package)
        {
            package = null;

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            var type = ResolveType(typeName);
            if (type == null)
            {
                typeof(PersistencePackageFactory).LogError($"Could not resolve package type: {typeName}");
                return false;
            }

            if (!typeof(IPersistencePackage).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
            {
                typeof(PersistencePackageFactory).LogError($"Invalid persistence package type: {type.FullName}");
                return false;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                typeof(PersistencePackageFactory).LogError(
                    $"Persistence package type requires a public parameterless constructor: {type.FullName}");
                return false;
            }

            if (Activator.CreateInstance(type) is not IPersistencePackage createdPackage)
            {
                typeof(PersistencePackageFactory).LogError($"Failed to create persistence package: {type.FullName}");
                return false;
            }

            package = createdPackage;
            return true;
        }

        private static Type ResolveType(string typeName) => LegacyTypeResolver.Resolve(typeName);
    }
}
