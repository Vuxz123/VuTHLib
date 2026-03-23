using System.IO;
using _VuTH.Common.Log;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _VuTH.Core.Persistant.DataPackage
{
    public static class DataPackageProfileUtilities
    {
        public static bool TryGetProfile(out DataPackageProfile profile)
        {
            profile = null;

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                profile = Resources.Load<DataPackageProfile>(DataPackageConstants.ProfilePath);
                return profile != null;
            }

            profile = AssetDatabase.LoadAssetAtPath<DataPackageProfile>(DataPackageConstants.AbsoluteProfilePath);
            if (profile) return true;

            typeof(DataPackageProfileUtilities).LogWarning(
                $"Creating new profile at: {DataPackageConstants.AbsoluteProfilePath}");

            EnsureFolderExists(DataPackageConstants.AbsoluteProfilePath);

            profile = ScriptableObject.CreateInstance<DataPackageProfile>();
            AssetDatabase.CreateAsset(profile, DataPackageConstants.AbsoluteProfilePath);
            AssetDatabase.SaveAssets();
            return true;
#else
            profile = Resources.Load<DataPackageProfile>(DataPackageConstants.ProfilePath);
            return profile != null;
#endif
        }

#if UNITY_EDITOR
        private static void EnsureFolderExists(string assetPath)
        {
            var folderPath = Path.GetDirectoryName(assetPath);
            if (Directory.Exists(folderPath)) return;
            if (folderPath != null) Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }
#endif
    }
}
