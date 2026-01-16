using System;
using System.Collections.Generic;
using Core.GameCycle.ScreenFlow.Condition;

namespace Core.GameCycle.ScreenFlow
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
            if (_graph == null) return null;
            if (string.IsNullOrWhiteSpace(_graph.StartNodeGuid)) return null;
            return _nodeByGuid.TryGetValue(_graph.StartNodeGuid, out var node) ? node : null;
        }

        public IReadOnlyList<ScreenFlowTransition> GetAvailableTransitions(ScreenFlowNode fromNode, string eventName)
        {
            if (fromNode == null) return Array.Empty<ScreenFlowTransition>();
            if (string.IsNullOrWhiteSpace(eventName)) return Array.Empty<ScreenFlowTransition>();

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

            for (var i = 0; i < transitions.Count; i++)
            {
                var t = transitions[i];
                if (t == null) continue;

                if (!Evaluate(t.Condition))
                    continue;

                if (string.IsNullOrWhiteSpace(t.ToNodeGuid))
                    return false;

                if (_nodeByGuid.TryGetValue(t.ToNodeGuid, out var toNode) && toNode != null)
                {
                    nextNode = toNode;
                    return true;
                }

                return false;
            }

            return false;
        }

        private static bool Evaluate(TransitionCondition condition)
        {
            // Policy: null condition is ALWAYS TRUE.
            if (condition == null) return true;

            try
            {
                return condition.Evaluate();
            }
            catch
            {
                // Fail-safe: if condition throws, treat as false.
                return false;
            }
        }

        private void BuildIndex()
        {
            _nodeByGuid.Clear();
            _transitionsByKey.Clear();

            if (_graph == null) return;

            if (_graph.Nodes != null)
            {
                for (var i = 0; i < _graph.Nodes.Count; i++)
                {
                    var n = _graph.Nodes[i];
                    if (n == null) continue;
                    if (string.IsNullOrWhiteSpace(n.Guid)) continue;

                    _nodeByGuid[n.Guid] = n;
                }
            }

            if (_graph.Transitions != null)
            {
                for (var i = 0; i < _graph.Transitions.Count; i++)
                {
                    var t = _graph.Transitions[i];
                    if (t == null) continue;
                    if (string.IsNullOrWhiteSpace(t.FromNodeGuid)) continue;
                    if (string.IsNullOrWhiteSpace(t.EventName)) continue;

                    var key = (t.FromNodeGuid, t.EventName);
                    if (!_transitionsByKey.TryGetValue(key, out var list))
                    {
                        list = new List<ScreenFlowTransition>(4);
                        _transitionsByKey[key] = list;
                    }

                    // Preserve graph order.
                    list.Add(t);
                }
            }
        }
    }
}

