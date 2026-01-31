using _VuTH.Core.GameCycle.Screen;
using _VuTH.Core.GameCycle.Screen.Core;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    // Proxy object so we can edit a node (Screen reference, start flag) in the inspector
    // while still persisting data back into ScreenFlowGraph.
    internal sealed class ScreenNodeSelectionProxy : ScriptableObject
    {
        public ScreenFlowGraph graph;
        public string nodeGuid;

        [Header("Node")]
        public ScreenModel screen;

        public static ScreenNodeSelectionProxy Create(ScreenFlowGraph graph, ScreenFlowNode node)
        {
            var proxy = CreateInstance<ScreenNodeSelectionProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave;
            proxy.graph = graph;
            proxy.nodeGuid = node.Guid;
            proxy.screen = node.Screen;
            return proxy;
        }

        private void OnValidate()
        {
            PushToGraph();
        }

        private void PushToGraph()
        {
            if (!graph || string.IsNullOrEmpty(nodeGuid))
                return;

            Undo.RecordObject(graph, "Edit Screen Node");
            var so = new SerializedObject(graph);
            so.Update();
            var nodesProp = so.FindProperty("nodes");
            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                var n = nodesProp.GetArrayElementAtIndex(i);
                if (n.FindPropertyRelative("guid").stringValue != nodeGuid)
                    continue;

                n.FindPropertyRelative("screen").objectReferenceValue = screen;
                break;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(graph);

            // Nudge editor selection so the window can refresh the view (GraphView reads from graph).
            Selection.activeObject = graph;
        }

        public void PingScreen()
        {
            if (!screen)
                return;
            EditorGUIUtility.PingObject(screen);
            Selection.activeObject = screen;
        }

        public void SetAsStartNode()
        {
            if (!graph || string.IsNullOrEmpty(nodeGuid))
                return;

            Undo.RecordObject(graph, "Set Start Node");
            graph.SetStartNode(nodeGuid);
            EditorUtility.SetDirty(graph);

            Selection.activeObject = graph;
        }

        public string Guid => nodeGuid;
        public bool IsStartNode => graph != null && graph.StartNodeGuid == nodeGuid;
        public ScreenModel Screen => screen;
    }
}
