#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VuTH.Editor
{
    /// <summary>
    /// Exports VuTH Lib as a single .unitypackage.
    /// 
    /// Menu: Window > VuTH > Export Package
    /// </summary>
    public static class PackageExporter
    {
        private const string PackageName = "VuTH.Lib";
        private const string SourcePath = "Assets/_VuTH";

        [MenuItem("Window/VuTH/Export Package", priority = 100)]
        public static void Build()
        {
            var repoRoot = Path.GetDirectoryName(Application.dataPath);
            var defaultPath = Path.Combine(repoRoot, $"{PackageName}.unitypackage");

            var path = EditorUtility.SaveFilePanel(
                "Export VuTH Lib",
                repoRoot,
                PackageName,
                "unitypackage"
            );

            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("[PackageExporter] Cancelled.");
                return;
            }

            ExportToPath(SourcePath, path);
        }

        private static void ExportToPath(string source, string outputPath)
        {
            var fullSourcePath = Path.Combine(Application.dataPath, "..", source);
            if (!Directory.Exists(fullSourcePath))
            {
                Debug.LogError($"[PackageExporter] Source path not found: {fullSourcePath}");
                return;
            }

            Debug.Log($"[PackageExporter] Exporting {source} → {outputPath}");

            AssetDatabase.ExportPackage(
                source,
                outputPath,
                ExportPackageOptions.Recurse
            );

            Debug.Log($"[PackageExporter] Done: {outputPath}");
            EditorUtility.RevealInFinder(outputPath);
        }
    }
}
#endif
