using System.IO;
using _VuTH.Common.Log;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _VuTH.Core.Window.Profile
{
    public static class WindowProfileUtilities
    {
        /// <summary>
        /// The profile in Resources. Outside play mode in the Editor it is created when missing (for the settings tab).
        /// </summary>
        public static bool TryGetProfile(out WindowProfile profile)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                profile = AssetDatabase.LoadAssetAtPath<WindowProfile>(WindowManagerConst.AbsoluteWindowProfilePath);
                if (profile) return true;

                typeof(WindowProfileUtilities).LogWarning(
                    $"Creating new profile at: {WindowManagerConst.AbsoluteWindowProfilePath}");
                EnsureFolderExists(WindowManagerConst.AbsoluteWindowProfilePath);
                profile = ScriptableObject.CreateInstance<WindowProfile>();
                AssetDatabase.CreateAsset(profile, WindowManagerConst.AbsoluteWindowProfilePath);
                AssetDatabase.SaveAssets();
                return true;
            }
#endif
            profile = Resources.Load<WindowProfile>(WindowManagerConst.WindowProfilePath);
            return profile != null;
        }

#if UNITY_EDITOR
        private static void EnsureFolderExists(string assetPath)
        {
            var folderPath = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(folderPath) || Directory.Exists(folderPath)) return;
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }
#endif
    }
}
