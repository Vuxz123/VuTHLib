using System;
using System.Collections.Generic;
using _VuTH.Core.GameCycle.ScreenFlow.Condition;
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

            foreach (var t in transitions.AsValueEnumerable()
                         .Where(t => t != null)
                         .Where(t => Evaluate(t.Condition)))
            {
                if (string.IsNullOrWhiteSpace(t.ToNodeGuid))
                    return false;

                if (!_nodeByGuid.TryGetValue(t.ToNodeGuid, out var toNode) || toNode == null) return false;
                nextNode = toNode;
                return true;

            }

            return false;
        }

        private static bool Evaluate(TransitionCondition condition)
        {
            // Policy: null condition is ALWAYS TRUE.
            if (!condition) return true;

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

                    // Preserve graph order.
                    list.Add(t);
                }
            }
        }
    }
}

