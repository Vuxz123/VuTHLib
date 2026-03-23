using System;
using System.Collections.Generic;
using System.Linq;
using _VuTH.Core.GameCycle.ScreenFlow.Editor.Validator;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using ZLinq;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    internal sealed class ScreenFlowEdgeHandler
    {
        private readonly Dictionary<ScreenFlowTransition, Edge> _edgeViewsByTransition = new();
        private readonly ScreenFlowGraphMutator _mutator;
        private readonly ScreenFlowGraph _graph;
        private Action _onSelectionChanged;

        public ScreenFlowEdgeHandler(ScreenFlowGraph graph, ScreenFlowGraphMutator mutator)
        {
            _graph = graph;
            _mutator = mutator;
        }

        public void SetOnSelectionChanged(Action callback)
        {
            _onSelectionChanged = callback;
        }

        public void ClearEdges()
        {
            _edgeViewsByTransition.Clear();
        }

        public void RegisterEdge(Edge edge, ScreenFlowTransition transition)
        {
            edge.userData = transition;
            edge.Add(new ScreenTransitionLabel(edge, transition));
            edge.RegisterCallback<MouseDownEvent>(OnEdgeMouseDown, TrickleDown.TrickleDown);
            _edgeViewsByTransition[transition] = edge;
        }

        public Edge GetEdgeForTransition(ScreenFlowTransition transition)
        {
            return _edgeViewsByTransition.GetValueOrDefault(transition);
        }

        public void OnTransitionCreated(string fromNodeGuid, string toNodeGuid)
        {
            _mutator.CreateTransition(fromNodeGuid, toNodeGuid);
        }

        public void OnTransitionRemoved(Edge edge)
        {
            if (edge.userData is ScreenFlowTransition transition)
            {
                if (_graph.Transitions.Contains(transition))
                {
                    _mutator.RemoveTransitionByGuid(transition.Guid);
                    return;
                }
            }

            string fromGuid = null;
            string toGuid = null;
            if (edge.output?.node is ScreenNodeView fromNode)
                fromGuid = fromNode.Guid;
            if (edge.input?.node is ScreenNodeView toNode)
                toGuid = toNode.Guid;

            _mutator.TryRemoveTransitionByGuidFallback(
                edge.userData is ScreenFlowTransition t ? t.Guid : null,
                fromGuid,
                toGuid);
        }

        private void OnEdgeMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not Edge edge) return;

            var graphView = edge.GetFirstAncestorOfType<ScreenFlowGraphView>();
            graphView?.ClearSelection();
            graphView?.AddToSelection(edge);
            _onSelectionChanged?.Invoke();
            evt.StopPropagation();
        }

        public void ValidateGraph()
        {
            if (!_graph) return;

            var report = ScreenFlowValidator.Validate(_graph);
            if (report.Count == 0)
            {
                EditorUtility.DisplayDialog("ScreenFlow Graph Validation", "Graph is valid.", "OK");
                return;
            }

            var messages = new List<string>(report.Count);
            messages.AddRange(report.Select(t => t.ToString()));

            EditorUtility.DisplayDialog("ScreenFlow Graph Validation", string.Join("\n", messages), "OK");
        }

        /// <summary>
        /// Forces all edge labels to recalculate their positions.
        /// Call this after nodes are dragged (detected via OnGraphViewChanged.movedElements).
        /// </summary>
        public void RefreshAllEdgeLabelPositions()
        {
            foreach (var label in _edgeViewsByTransition.AsValueEnumerable()
                         .Select(kvp => kvp.Value)
                         .Select(edge => edge.Q<ScreenTransitionLabel>()))
            {
                label?.UpdatePosition();
            }
        }
    }
}
