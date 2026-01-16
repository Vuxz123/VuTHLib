using System.Linq;
using UnityEditor;

namespace Core.GameCycle.ScreenFlow.Editor.Discovery
{
    public static class ScreenFlowDiscovery
    {
        public static ScreenFlowGraph TryFindRootFlow(out string error)
        {
            error = null;

            // Find all ScreenFlowGraph assets in project
            var guids = AssetDatabase.FindAssets("t:ScreenFlowGraph");

            if (guids == null || guids.Length == 0)
            {
                error =
                    "[ScreenFlow] No ScreenFlowGraph found in project.\n" +
                    "Exactly ONE ScreenFlowGraph is required to define the game flow.";
                return null;
            }

            if (guids.Length > 1)
            {
                var paths = guids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToArray();

                error =
                    "[ScreenFlow] Multiple ScreenFlowGraph assets found.\n" +
                    "Exactly ONE ScreenFlowGraph is allowed.\n\n" +
                    string.Join("\n", paths);

                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var graph = AssetDatabase.LoadAssetAtPath<ScreenFlowGraph>(path);

            if (!graph)
            {
                error =
                    $"[ScreenFlow] Failed to load ScreenFlowGraph at path:\n{path}";
                return null;
            }

            return graph;
        }
    }
}