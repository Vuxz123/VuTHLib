using _VuTH.Core.GameCycle.ScreenFlow.Condition;
using _VuTH.Core.GameCycle.ScreenFlow.Editor;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    /// <summary>
    /// A small ScriptableObject wrapper so we can edit one transition via inspector
    /// while still writing changes back into the ScreenFlowGraph serialized data.
    /// </summary>
    internal sealed class ScreenTransitionSelectionProxy : ScriptableObject
    {
        [SerializeField] private ScreenFlowGraph graph;
        [SerializeField] private int transitionIndex = -1;
        [SerializeField] private string fromGuid;
        [SerializeField] private string toGuid;

        [Header("Transition")]
        [SerializeField] private string eventName;
        [SerializeField] private TransitionCondition condition;

        public static ScreenTransitionSelectionProxy Create(ScreenFlowGraph graph, ScreenFlowTransition transition)
        {
            var proxy = CreateInstance<ScreenTransitionSelectionProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave;
            proxy.graph = graph;
            proxy.transitionIndex = FindTransitionIndex(graph, transition);
            proxy.fromGuid = transition.FromNodeGuid;
            proxy.toGuid = transition.ToNodeGuid;
            proxy.eventName = transition.EventName;
            proxy.condition = transition.Condition;
            return proxy;
        }

        internal void ApplyToGraph()
        {
            PushToGraph();
        }

        private void PushToGraph()
        {
            if (!graph)
                return;

            Undo.RecordObject(graph, "Edit Transition");
            var so = new SerializedObject(graph);
            so.Update();
            var transitionsProp = so.FindProperty("transitions");

            if (transitionIndex >= 0 && transitionIndex < transitionsProp.arraySize)
            {
                var transitionProperty = transitionsProp.GetArrayElementAtIndex(transitionIndex);
                transitionProperty.FindPropertyRelative("eventName").stringValue = eventName;
                transitionProperty.FindPropertyRelative("condition").objectReferenceValue = condition;
            }
            else
            {
                for (var i = 0; i < transitionsProp.arraySize; i++)
                {
                    var transitionProperty = transitionsProp.GetArrayElementAtIndex(i);
                    var from = transitionProperty.FindPropertyRelative("fromNodeGuid").stringValue;
                    var to = transitionProperty.FindPropertyRelative("toNodeGuid").stringValue;
                    if (from != fromGuid || to != toGuid)
                        continue;

                    transitionIndex = i;
                    transitionProperty.FindPropertyRelative("eventName").stringValue = eventName;
                    transitionProperty.FindPropertyRelative("condition").objectReferenceValue = condition;
                    break;
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(graph);
            ScreenFlowGraphEditorWindow.NotifyGraphChanged(graph);
        }

        private static int FindTransitionIndex(ScreenFlowGraph graph, ScreenFlowTransition transition)
        {
            if (!graph || transition == null) return -1;

            for (var i = 0; i < graph.Transitions.Count; i++)
            {
                if (ReferenceEquals(graph.Transitions[i], transition))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
