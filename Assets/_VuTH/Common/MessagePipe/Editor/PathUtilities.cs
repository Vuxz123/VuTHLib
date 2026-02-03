using UnityEditor;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Utility methods for path operations in the Unity Editor.
    /// </summary>
    internal static class PathUtilities
    {
        /// <summary>
        /// Ensure all folders in the path exist, creating them if necessary.
        /// </summary>
        public static void EnsureFolderExists(string folderPath)
        {
            var parts = folderPath.Replace("\\", "/").Split('/');
            var currentPath = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                var nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                }
                currentPath = nextPath;
            }
        }
    }
}
