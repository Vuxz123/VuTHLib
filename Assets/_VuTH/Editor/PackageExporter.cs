#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VuTH.Editor
{
    /// <summary>
    /// Command-line package exporter for CI/CD pipelines.
    /// 
    /// Usage from command line (PowerShell):
    ///   & Unity.exe -projectPath . -quit -batchmode -nographics `
    ///     -executeMethod PackageExporter.ExportPackage `
    ///     -packagePath="Assets/_VuTH/Core/Audio" `
    ///     -outputPath="build-output/VuTH.Audio.unitypackage"
    /// </summary>
    public static class PackageExporter
    {
        [MenuItem("VuTH/Export All Packages")]
        public static void ExportAllPackages()
        {
            var packages = new[]
            {
                ("Assets/_VuTH/Core/Audio", "VuTH.Audio"),
                ("Assets/_VuTH/Core/Window", "VuTH.Window"),
                ("Assets/_VuTH/Core/Window.Transition", "VuTH.Window.Transition"),
                ("Assets/_VuTH/Core/Screen", "VuTH.Screen"),
                ("Assets/_VuTH/Core/ScreenFlow", "VuTH.ScreenFlow"),
                ("Assets/_VuTH/Core/Pool", "VuTH.Pool"),
                ("Assets/_VuTH/Core/Bootstrap", "VuTH.Bootstrap"),
                ("Assets/_VuTH/Core/GameCycle", "VuTH.GameCycle"),
                ("Assets/_VuTH/Core/Persistant", "VuTH.Persistant"),
            };

            string outputDir = Path.Combine(Application.dataPath, "..", "build-output");
            Directory.CreateDirectory(outputDir);

            foreach (var (path, name) in packages)
            {
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                if (!Directory.Exists(fullPath))
                {
                    Debug.LogWarning($"[PackageExporter] Skipping {name} — path not found: {path}");
                    continue;
                }

                string output = Path.Combine(outputDir, $"{name}.unitypackage");
                AssetDatabase.ExportPackage(
                    path,
                    output,
                    ExportPackageOptions.Recurse
                );
                Debug.Log($"[PackageExporter] Exported: {output}");
            }

            Debug.Log("[PackageExporter] All packages exported.");
        }

        // Called from CI command line
        public static void ExportPackage(string packagePath, string outputPath)
        {
            if (string.IsNullOrEmpty(packagePath) || string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException("packagePath and outputPath are required.");
            }

            string fullSourcePath = Path.Combine(Application.dataPath, "..", packagePath);
            if (!Directory.Exists(fullSourcePath))
            {
                throw new IOException($"Package path not found: {fullSourcePath}");
            }

            // Resolve output path — if relative, place next to project root
            string outputFullPath = outputPath;
            if (!Path.IsPathRooted(outputFullPath))
            {
                outputFullPath = Path.Combine(Application.dataPath, "..", outputPath);
            }

            string outputDir = Path.GetDirectoryName(outputFullPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            AssetDatabase.ExportPackage(
                packagePath,
                outputFullPath,
                ExportPackageOptions.Recurse
            );

            Debug.Log($"[PackageExporter] Exported {packagePath} → {outputFullPath}");
            Console.WriteLine($"[PackageExporter] Exported {packagePath} → {outputFullPath}");
        }
    }
}
#endif
