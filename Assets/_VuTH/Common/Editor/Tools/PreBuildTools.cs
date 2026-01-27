using _VuTH.Common.Log;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Tools
{
    public static class PreBuildTools
    {
        [MenuItem("VuTH/PreBuild/Run All PreBuild Tasks")]
        public static void RunAllPreBuildTasks()
        {
            // Find all pre build profiles and run their tasks
            var profiles = Resources.FindObjectsOfTypeAll<PreBuildProfile>();
            if (profiles == null || profiles.Length == 0)
            {
                LogUtils.Log("[PreBuild] No PreBuildProfile found.", Color.yellow);
                return;
            }
            
            foreach (var profile in profiles)
            {
                try
                {
                    profile.ExecuteAllTasks();
                    LogUtils.Log("[PreBuild] " + profile.name, Color.green);
                }
                catch (System.Exception ex)
                {
                    LogUtils.LogError("[PreBuild] Error executing profile " + profile.name + ": " + ex.Message, Color.red);
                    Debug.LogException(ex, profile);
                }
            }
        }
    }
}