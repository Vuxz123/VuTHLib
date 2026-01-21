using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Common.Editor.Tools.Podfile
{
    public abstract class PodfileTask : IPreBuildTask
    {
        private const string PodfileRelativeToAssets = "Editor/IOSBuildTool/Podfile";

        public void Execute()
        {
            var fullPath = GetFullPodfilePath();
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Podfile not found at path: {fullPath}", fullPath);
            }

            // Open for read/write so tasks can update in-place if they really want to.
            // Prefer using ReadAllText/WriteAllText helpers below for atomic updates.
            using (var podfile = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                ProcessPodfile(podfile);
            }
        }

        public abstract void ProcessPodfile(FileStream podfile);

        protected static string GetFullPodfilePath()
        {
            // Application.dataPath points to <project>/Assets
            return Path.GetFullPath(Path.Combine(Application.dataPath, PodfileRelativeToAssets));
        }

        protected static string ReadAllText(string fullPath)
        {
            // Podfile is plain text. Use UTF8 without BOM by default.
            return File.ReadAllText(fullPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        protected static void WriteAllTextAtomic(string fullPath, string content)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException($"Invalid Podfile path: {fullPath}");

            Directory.CreateDirectory(directory);

            var tempPath = fullPath + ".tmp";
            File.WriteAllText(tempPath, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Replace existing file atomically when possible.
            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
    }
}