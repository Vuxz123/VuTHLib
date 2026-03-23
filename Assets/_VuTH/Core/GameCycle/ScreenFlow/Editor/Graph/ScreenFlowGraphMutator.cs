using System;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Core.A;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    internal sealed class ScreenFlowGraphMutator
    {
        private readonly ScreenFlowGraph _graph;
        private Action _onGraphChanged;

        public string PendingSelectedNodeGuid { get; internal set; }
        public ScreenFlowTransition PendingSelectedTransition { get; internal set; }

        public ScreenFlowGraphMutator(ScreenFlowGraph graph, Action onGraphChanged)
        {
            _graph = graph;
            _onGraphChanged = onGraphChanged;
        }

        public void SetOnGraphChanged(Action callback)
        {
            _onGraphChanged = callback;
        }

        public void AddNode(Vector2 position, ScreenModel screen)
        {
            if (!_graph) return;

            Undo.RecordObject(_graph, "Add Screen Node");
            var serializedGraph = new SerializedObject(_graph);
            serializedGraph.Update();

            var nodesProperty = serializedGraph.FindProperty("nodes");
            var newIndex = nodesProperty.arraySize;
            nodesProperty.arraySize++;

            var nodeProperty = nodesProperty.GetArrayElementAtIndex(newIndex);
            nodeProperty.FindPropertyRelative("guid").stringValue = Guid.NewGuid().ToString();
            nodeProperty.FindPropertyRelative("screen").objectReferenceValue = screen;
            nodeProperty.FindPropertyRelative("editorPosition").vector2Value = position;

            serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);

            if (newIndex >= 0 && newIndex < _graph.Nodes.Count)
            {
                PendingSelectedNodeGuid = _graph.Nodes[newIndex].Guid;
            }

            _onGraphChanged?.Invoke();
        }

        public void RemoveNode(string nodeGuid)
        {
            if (!_graph) return;

            Undo.RecordObject(_graph, "Remove Screen Node");
            var serializedGraph = new SerializedObject(_graph);
            serializedGraph.Update();

            var transitionsProperty = serializedGraph.FindProperty("transitions");
            for (var i = transitionsProperty.arraySize - 1; i >= 0; i--)
            {
                var transitionProperty = transitionsProperty.GetArrayElementAtIndex(i);
                var fromNodeGuid = transitionProperty.FindPropertyRelative("fromNodeGuid").stringValue;
                var toNodeGuid = transitionProperty.FindPropertyRelative("toNodeGuid").stringValue;
                if (fromNodeGuid == nodeGuid || toNodeGuid == nodeGuid)
                {
                    transitionsProperty.DeleteArrayElementAtIndex(i);
                }
            }

            var nodesProperty = serializedGraph.FindProperty("nodes");
            for (var i = 0; i < nodesProperty.arraySize; i++)
            {
                var nodeProperty = nodesProperty.GetArrayElementAtIndex(i);
                if (nodeProperty.FindPropertyRelative("guid").stringValue != nodeGuid) continue;

                nodesProperty.DeleteArrayElementAtIndex(i);
                break;
            }

            var startNodeProperty = serializedGraph.FindProperty("startNodeGuid");
            if (startNodeProperty.stringValue == nodeGuid)
            {
                startNodeProperty.stringValue = string.Empty;
            }

            serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);

            PendingSelectedNodeGuid = null;
            PendingSelectedTransition = null;
            _onGraphChanged?.Invoke();
        }

        public void SetStartNode(string nodeGuid)
        {
            if (!_graph) return;

            Undo.RecordObject(_graph, "Set Start Node");
            _graph.SetStartNode(nodeGuid);
            EditorUtility.SetDirty(_graph);

            PendingSelectedNodeGuid = nodeGuid;
            _onGraphChanged?.Invoke();
        }

        public void CreateTransition(string fromNodeGuid, string toNodeGuid)
        {
            if (!_graph) return;

            Undo.RecordObject(_graph, "Add Transition");
            var serializedGraph = new SerializedObject(_graph);
            serializedGraph.Update();

            var transitionsProperty = serializedGraph.FindProperty("transitions");
            var newIndex = transitionsProperty.arraySize;
            transitionsProperty.arraySize++;

            var transitionProperty = transitionsProperty.GetArrayElementAtIndex(newIndex);
            transitionProperty.FindPropertyRelative("guid").stringValue = Guid.NewGuid().ToString();
            transitionProperty.FindPropertyRelative("fromNodeGuid").stringValue = fromNodeGuid;
            transitionProperty.FindPropertyRelative("toNodeGuid").stringValue = toNodeGuid;
            transitionProperty.FindPropertyRelative("eventName").stringValue = string.Empty;
            transitionProperty.FindPropertyRelative("condition").objectReferenceValue = null;

            serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);

            if (newIndex >= 0 && newIndex < _graph.Transitions.Count)
            {
                PendingSelectedTransition = _graph.Transitions[newIndex];
            }

            _onGraphChanged?.Invoke();
        }

        public void RemoveTransitionByGuid(string transitionGuid)
        {
            if (!_graph || string.IsNullOrEmpty(transitionGuid)) return;

            Undo.RecordObject(_graph, "Remove Transition");
            var serializedGraph = new SerializedObject(_graph);
            serializedGraph.Update();

            var transitionsProperty = serializedGraph.FindProperty("transitions");
            for (var i = 0; i < transitionsProperty.arraySize; i++)
            {
                var transitionProperty = transitionsProperty.GetArrayElementAtIndex(i);
                if (transitionProperty.FindPropertyRelative("guid").stringValue != transitionGuid) continue;

                transitionsProperty.DeleteArrayElementAtIndex(i);
                serializedGraph.ApplyModifiedProperties();
                EditorUtility.SetDirty(_graph);
                PendingSelectedTransition = null;
                _onGraphChanged?.Invoke();
                return;
            }
        }

        public bool TryRemoveTransitionByGuidFallback(string transitionGuid, string fromNodeGuid, string toNodeGuid)
        {
            if (!_graph) return false;

            Undo.RecordObject(_graph, "Remove Transition");
            var serializedGraph = new SerializedObject(_graph);
            serializedGraph.Update();

            var transitionsProperty = serializedGraph.FindProperty("transitions");
            var removed = false;

            for (var i = 0; i < transitionsProperty.arraySize; i++)
            {
                var transitionProperty = transitionsProperty.GetArrayElementAtIndex(i);
                var guid = transitionProperty.FindPropertyRelative("guid").stringValue;
                if (guid == transitionGuid)
                {
                    transitionsProperty.DeleteArrayElementAtIndex(i);
                    removed = true;
                    break;
                }
            }

            if (!removed)
            {
                for (var i = 0; i < transitionsProperty.arraySize; i++)
                {
                    var transitionProperty = transitionsProperty.GetArrayElementAtIndex(i);
                    if (transitionProperty.FindPropertyRelative("fromNodeGuid").stringValue != fromNodeGuid) continue;
                    if (transitionProperty.FindPropertyRelative("toNodeGuid").stringValue != toNodeGuid) continue;

                    transitionsProperty.DeleteArrayElementAtIndex(i);
                    removed = true;
                    break;
                }
            }

            if (!removed) return false;

            serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
            PendingSelectedTransition = null;
            _onGraphChanged?.Invoke();
            return true;
        }

        public void PersistNodePosition(string nodeGuid, Vector2 position)
        {
            if (!_graph) return;

            Undo.RecordObject(_graph, "Move Screen Node");
            var serializedGraph = new SerializedObject(_graph);
            serializedGraph.Update();

            var nodesProperty = serializedGraph.FindProperty("nodes");
            for (var i = 0; i < nodesProperty.arraySize; i++)
            {
                var nodeProperty = nodesProperty.GetArrayElementAtIndex(i);
                if (nodeProperty.FindPropertyRelative("guid").stringValue != nodeGuid) continue;

                nodeProperty.FindPropertyRelative("editorPosition").vector2Value = position;
                break;
            }

            serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
        }

        public void SetNodeScreen(string nodeGuid, ScreenModel screen)
        {
            if (!_graph) return;

            Undo.RecordObject(_graph, "Set Node Screen");
            var serializedGraph = new SerializedObject(_graph);
            serializedGraph.Update();

            var nodesProperty = serializedGraph.FindProperty("nodes");
            for (var i = 0; i < nodesProperty.arraySize; i++)
            {
                var nodeProperty = nodesProperty.GetArrayElementAtIndex(i);
                if (nodeProperty.FindPropertyRelative("guid").stringValue != nodeGuid) continue;

                nodeProperty.FindPropertyRelative("screen").objectReferenceValue = screen;
                break;
            }

            serializedGraph.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);

            PendingSelectedNodeGuid = nodeGuid;
            _onGraphChanged?.Invoke();
        }
    }
}
