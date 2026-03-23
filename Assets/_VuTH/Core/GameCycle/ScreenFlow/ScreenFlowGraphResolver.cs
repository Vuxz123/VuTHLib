using System;
using System.Collections.Generic;
using _VuTH.Core.GameCycle.ScreenFlow.Condition;
using UnityEngine;
using ZLinq;

namespace _VuTH.Core.GameCycle.ScreenFlow
{
    /// <summary>
    /// Pure resolver: reads ScreenFlowGraph and resolves next node for a given (current,event).
    /// Rules:
    /// - Evaluate transitions in graph order.
    /// - TransitionCondition == null => always true.
    /// - First condition-true wins.
    /// </summary>
    public sealed class ScreenFlowGraphResolver : IScreenFlowResolver
    {
        private readonly ScreenFlowGraph _graph;

        private readonly Dictionary<string, ScreenFlowNode> _nodeByGuid = new(StringComparer.Ordinal);
        private readonly Dictionary<(string From, string Event), List<ScreenFlowTransition>> _transitionsByKey = new();

        public ScreenFlowGraphResolver(ScreenFlowGraph graph)
        {
            _graph = graph;
            BuildIndex();
        }

        public ScreenFlowNode GetStartNode()
        {
            if (!_graph) return null;
            return string.IsNullOrWhiteSpace(_graph.StartNodeGuid) ? null : 
                _nodeByGuid.GetValueOrDefault(_graph.StartNodeGuid);
        }

        public IReadOnlyList<ScreenFlowTransition> GetAvailableTransitions(ScreenFlowNode fromNode, string eventName)
        {
            if (fromNode == null || string.IsNullOrWhiteSpace(eventName)) 
                return Array.Empty<ScreenFlowTransition>();

            return _transitionsByKey.TryGetValue((fromNode.Guid, eventName), out var list)
                ? list
                : Array.Empty<ScreenFlowTransition>();
        }

        public bool TryResolve(ScreenFlowNode currentNode, string eventName, out ScreenFlowNode nextNode)
        {
            nextNode = null;

            if (currentNode == null) return false;
            if (string.IsNullOrWhiteSpace(eventName)) return false;

            if (!_transitionsByKey.TryGetValue((currentNode.Guid, eventName), out var transitions) || transitions == null)
                return false;

            ScreenFlowNode firstMatch = null;
            bool multipleConditionsTrue = false;

            foreach (var t in transitions.AsValueEnumerable().Where(t => t != null))
            {
                if (!Evaluate(t.Condition)) continue;
                if (firstMatch == null)
                {
                    if (string.IsNullOrWhiteSpace(t.ToNodeGuid))
                        return false;

                    if (!_nodeByGuid.TryGetValue(t.ToNodeGuid, out var toNode) || toNode == null)
                        return false;
                    firstMatch = toNode;
                }
                else
                {
                    multipleConditionsTrue = true;
                }
            }

            if (multipleConditionsTrue)
            {
                Debug.LogWarning($"[ScreenFlowGraphResolver] Multiple conditions evaluated to true for event '{eventName}' from node '{currentNode.Guid}'. First matching transition wins deterministically. This may indicate unintended graph configuration.");
            }

            nextNode = firstMatch;
            return firstMatch != null;
        }

        private static bool Evaluate(TransitionCondition condition)
        {
            if (!condition)
            {
                Debug.LogError($"[ScreenFlowGraphResolver] TransitionCondition is null. A null condition should not silently pass.");
                return false;
            }

            try
            {
                return condition.Evaluate();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScreenFlowGraphResolver] Condition evaluation threw exception: {ex.Message}");
                return false;
            }
        }

        private void BuildIndex()
        {
            _nodeByGuid.Clear();
            _transitionsByKey.Clear();

            if (!_graph) return;

            if (_graph.Nodes != null)
            {
                foreach (var n in _graph.Nodes)
                {
                    if (n == null) continue;
                    if (string.IsNullOrWhiteSpace(n.Guid)) continue;

                    _nodeByGuid[n.Guid] = n;
                }
            }

            if (_graph.Transitions != null)
            {
                foreach (var t in _graph.Transitions)
                {
                    if (t == null) continue;
                    if (string.IsNullOrWhiteSpace(t.FromNodeGuid)) continue;
                    if (string.IsNullOrWhiteSpace(t.EventName)) continue;

                    var key = (t.FromNodeGuid, t.EventName);
                    if (!_transitionsByKey.TryGetValue(key, out var list))
                    {
                        list = new List<ScreenFlowTransition>(4);
                        _transitionsByKey[key] = list;
                    }

                    list.Add(t);
                }

                DetectAmbiguousTransitions();
            }
        }

        private void DetectAmbiguousTransitions()
        {
            foreach (var kvp in _transitionsByKey)
            {
                var (fromGuid, eventName) = kvp.Key;
                var transitions = kvp.Value;

                int nullConditionCount = 0;
                foreach (var t in transitions)
                {
                    if (t != null && !t.Condition)
                    {
                        nullConditionCount++;
                    }
                }

                if (nullConditionCount > 1)
                {
                    Debug.LogWarning($"[ScreenFlowGraphResolver] Multiple unconditional transitions (null conditions) for event '{eventName}' from node '{fromGuid}'. Only the first in graph order will be selected.");
                }
            }
        }
    }
}

