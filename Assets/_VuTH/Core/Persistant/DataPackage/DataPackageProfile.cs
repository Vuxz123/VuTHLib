using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Persistant.DataPackage
{
    public class DataPackageProfile : ScriptableObject
    {
        [SerializeField] private List<string> packageTypeNames = new();

        public IReadOnlyList<string> PackageTypeNames => packageTypeNames;

        public void SetPackageTypeNames(IEnumerable<string> typeNames)
        {
            packageTypeNames.Clear();

            var seen = new HashSet<string>();
            foreach (var typeName in typeNames)
            {
                if (string.IsNullOrWhiteSpace(typeName)) continue;
                if (!seen.Add(typeName)) continue;
                packageTypeNames.Add(typeName);
            }
        }
    }
}
