using _VuTH.Core.GameCycle.ScreenFlow.Condition;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    /// <summary>
    /// A small ScriptableObject wrapper so we can edit one transition via inspector
    /// while still writing changes back into the ScreenFlowGraph serialized data.
    /// <para>
    /// The transition is located by its GUID on every write (not by list index), so deleting or reordering other
    /// transitions while this inspector is open can no longer redirect edits to the wrong transition.
    /// </para>
    /// </summary>
    internal sealed class ScreenTransitionSelectionProxy : ScriptableObject
    {
        [SerializeField] private ScreenFlowGraph graph;
        [SerializeField] private string transitionGuid;
        [SerializeField] private string fromGuid;
        [SerializeField] private string toGuid;
        [SerializeField] private string originalEventName;

        [Header("Transition")]
        [SerializeField] private string eventName;
        [SerializeField] private TransitionCondition condition;
        [SerializeField] private ScreenFlowNavigation navigation;
        [SerializeField] private ScreenBelowVisibility screenBelow;

        internal ScreenFlowGraph Graph => graph;
        internal string TransitionGuid => transitionGuid;

        /// <summary>False once the transition no longer exists in the graph (deleted, undone...).</summary>
        internal bool IsValid
        {
            get
            {
                if (!graph) return false;
                using var so = new SerializedObject(graph);
                return FindIndex(so.FindProperty("transitions")) >= 0;
            }
        }

        public static ScreenTransitionSelectionProxy Create(ScreenFlowGraph graph, ScreenFlowTransition transition)
        {
            var proxy = CreateInstance<ScreenTransitionSelectionProxy>();
            proxy.hideFlags = HideFlags.HideAndDontSave;
            proxy.graph = graph;
            proxy.transitionGuid = transition.Guid;
            proxy.fromGuid = transition.FromNodeGuid;
            proxy.toGuid = transition.ToNodeGuid;
            proxy.originalEventName = transition.EventName;
            proxy.eventName = transition.EventName;
            proxy.condition = transition.Condition;
            proxy.navigation = transition.Navigation;
            proxy.screenBelow = transition.ScreenBelow;
            return proxy;
        }

        internal void ApplyToGraph()
        {
            if (!graph) return;

            var so = new SerializedObject(graph);
            so.Update();
            var transitionsProp = so.FindProperty("transitions");

            var index = FindIndex(transitionsProp);
            if (index < 0)
            {
                Debug.LogWarning("[ScreenFlow] The edited transition no longer exists in the graph; change ignored.");
                return;
            }

            var transitionProperty = transitionsProp.GetArrayElementAtIndex(index);
            transitionProperty.FindPropertyRelative("eventName").stringValue = eventName;
            transitionProperty.FindPropertyRelative("condition").objectReferenceValue = condition;
            transitionProperty.FindPropertyRelative("navigation").intValue = (int)navigation;
            transitionProperty.FindPropertyRelative("screenBelow").intValue = (int)screenBelow;

            // ApplyModifiedProperties registers the Undo step itself.
            so.ApplyModifiedProperties();
            originalEventName = eventName;
            ScreenFlowGraphEditorWindow.NotifyGraphChanged(graph);
        }

        private int FindIndex(SerializedProperty transitionsProp)
        {
            if (!graph || transitionsProp == null) return -1;

            for (var i = 0; i < transitionsProp.arraySize; i++)
            {
                var t = transitionsProp.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(transitionGuid))
                {
                    if (t.FindPropertyRelative("guid").stringValue == transitionGuid) return i;
                    continue;
                }

                // Legacy data without GUIDs: match the exact (from, to, event) triple.
                if (t.FindPropertyRelative("fromNodeGuid").stringValue == fromGuid &&
                    t.FindPropertyRelative("toNodeGuid").stringValue == toGuid &&
                    t.FindPropertyRelative("eventName").stringValue == originalEventName)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
