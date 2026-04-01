#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VuTH.Editor
{
    /// <summary>
    /// Exports VuTH Lib as a single .unitypackage to the repo root.
    /// 
    /// Local usage:
    ///   Unity menu: Window > VuTH > Export Package
    /// 
    /// CI usage (command line):
    ///   & Unity.exe -projectPath . -quit -batchmode -nographics `
    ///     -executeMethod PackageExporter.Build
    /// </summary>
    public static class PackageExporter
    {
        private const string PackageName = "VuTH.Lib";
        private const string SourcePath = "Assets/_VuTH";

        [MenuItem("Window/VuTH/Export Package", priority = 100)]
        public static void Build()
        {
            ExportVuTHPackage();
        }

        [MenuItem("Window/VuTH/Export Package (with Version)", priority = 100)]
        public static void BuildWithVersion()
        {
            var version = GetVersionFromArgs();
            var outputPath = Path.Combine(GetRepoRoot(), $"{PackageName}.{version}.unitypackage");
            ExportToPath(SourcePath, outputPath);
        }

        // Called from CI
        public static void CIBuild()
        {
            var version = GetVersionFromArgs();
            var outputPath = string.IsNullOrEmpty(version)
                ? Path.Combine(GetRepoRoot(), $"{PackageName}.unitypackage")
                : Path.Combine(GetRepoRoot(), $"{PackageName}.{version}.unitypackage");
            ExportToPath(SourcePath, outputPath);
        }

        private static void ExportVuTHPackage()
        {
            var version = GetVersionFromArgs();
            if (string.IsNullOrEmpty(version))
            {
                // Ask user
                var path = EditorUtility.SaveFilePanel(
                    "Export VuTH Lib",
                    Application.dataPath,
                    $"{PackageName}",
                    "unitypackage"
                );
                if (string.IsNullOrEmpty(path))
                    return;

                ExportToPath(SourcePath, path);
                return;
            }

            var outputPath = Path.Combine(GetRepoRoot(), $"{PackageName}.{version}.unitypackage");
            ExportToPath(SourcePath, outputPath);
        }

        private static void ExportToPath(string source, string outputPath)
        {
            if (!Directory.Exists(Path.Combine(Application.dataPath, "..", source)))
            {
                Debug.LogError($"[PackageExporter] Source path not found: {source}");
                return;
            }

            Debug.Log($"[PackageExporter] Exporting {source} → {outputPath}");

            AssetDatabase.ExportPackage(
                source,
                outputPath,
                ExportPackageOptions.Recurse
            );

            Debug.Log($"[PackageExporter] Done: {outputPath}");

            // Refresh to show new file
            AssetDatabase.Refresh();
        }

        private static string GetRepoRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        private static string GetVersionFromArgs()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-version" || args[i] == "-v")
                    return args[i + 1];
            }
            return null;
        }
    }
}
#endif
