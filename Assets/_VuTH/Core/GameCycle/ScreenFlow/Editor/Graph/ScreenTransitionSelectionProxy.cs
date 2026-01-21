using Core.GameCycle.ScreenFlow.Condition;
using UnityEditor;
using UnityEngine;

namespace Core.GameCycle.ScreenFlow.Editor.Graph
{
    /// <summary>
    /// A small ScriptableObject wrapper so we can edit one transition via inspector
    /// while still writing changes back into the ScreenFlowGraph serialized data.
    /// </summary>
    internal sealed class ScreenTransitionSelectionProxy : ScriptableObject
    {
        [SerializeField] private ScreenFlowGraph graph;
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
            for (var i = 0; i < transitionsProp.arraySize; i++)
            {
                var t = transitionsProp.GetArrayElementAtIndex(i);
                var from = t.FindPropertyRelative("fromNodeGuid").stringValue;
                var to = t.FindPropertyRelative("toNodeGuid").stringValue;
                if (from != fromGuid || to != toGuid)
                    continue;

                // Update first match (MVP). Later we can add a transition GUID.
                t.FindPropertyRelative("eventName").stringValue = eventName;
                t.FindPropertyRelative("condition").objectReferenceValue = condition;
                break;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(graph);
        }
    }
}
