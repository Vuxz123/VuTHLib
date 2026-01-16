namespace Core.GameCycle.ScreenFlow.Editor.Validator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum ScreenFlowValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public readonly struct ScreenFlowValidationIssue
    {
        public ScreenFlowValidationIssue(
            ScreenFlowValidationSeverity severity,
            string message,
            string nodeGuid = null,
            string fromNodeGuid = null,
            string toNodeGuid = null,
            string eventName = null)
        {
            Severity = severity;
            Message = message;
            NodeGuid = nodeGuid;
            FromNodeGuid = fromNodeGuid;
            ToNodeGuid = toNodeGuid;
            EventName = eventName;
        }

        public ScreenFlowValidationSeverity Severity { get; }
        public string Message { get; }

        // Optional metadata for debugging/inspector highlighting
        public string NodeGuid { get; }
        public string FromNodeGuid { get; }
        public string ToNodeGuid { get; }
        public string EventName { get; }

        public override string ToString()
        {
            var meta = string.Empty;
            if (!string.IsNullOrWhiteSpace(NodeGuid)) meta += $" node={NodeGuid}";
            if (!string.IsNullOrWhiteSpace(FromNodeGuid) || !string.IsNullOrWhiteSpace(ToNodeGuid))
                meta += $" from={FromNodeGuid} to={ToNodeGuid}";
            if (!string.IsNullOrWhiteSpace(EventName)) meta += $" event={EventName}";

            return $"[{Severity}] {Message}{meta}";
        }
    }

    public static class ScreenFlowValidator
    {
        /// <summary>
        /// Validate a ScreenFlowGraph asset.
        /// Notes:
        /// - TransitionCondition == null is considered valid (runtime treats it as always-true).
        /// - eventName empty is a BLOCKING error.
        /// - Multiple transitions with same (from,event) is a WARNING because runtime will pick the first true by order.
        /// </summary>
        public static IReadOnlyList<ScreenFlowValidationIssue> Validate(ScreenFlowGraph graph)
        {
            if (graph == null)
            {
                return new[]
                {
                    new ScreenFlowValidationIssue(ScreenFlowValidationSeverity.Error, "Graph is null")
                };
            }

            var issues = new List<ScreenFlowValidationIssue>(32);

            // --- Nodes ---
            var nodes = graph.Nodes ?? Array.Empty<ScreenFlowNode>();
            var transitions = graph.Transitions ?? Array.Empty<ScreenFlowTransition>();

            var guidSet = new HashSet<string>();
            var guidToNode = new Dictionary<string, ScreenFlowNode>();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Node at index {i} is null"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.Guid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Node at index {i} has empty Guid"));
                    continue;
                }

                if (!guidSet.Add(node.Guid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Duplicate node Guid '{node.Guid}'",
                        nodeGuid: node.Guid));
                    continue;
                }

                guidToNode[node.Guid] = node;

                if (node.Screen == null)
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Node '{node.Guid}' has null Screen",
                        nodeGuid: node.Guid));
                }
            }

            // --- Start ---
            if (string.IsNullOrWhiteSpace(graph.StartNodeGuid))
            {
                issues.Add(new ScreenFlowValidationIssue(
                    ScreenFlowValidationSeverity.Error,
                    "StartNodeGuid is empty"));
            }
            else if (!guidToNode.ContainsKey(graph.StartNodeGuid))
            {
                issues.Add(new ScreenFlowValidationIssue(
                    ScreenFlowValidationSeverity.Error,
                    $"StartNodeGuid '{graph.StartNodeGuid}' does not exist in Nodes",
                    nodeGuid: graph.StartNodeGuid));
            }

            // --- Transitions ---
            for (var i = 0; i < transitions.Count; i++)
            {
                var t = transitions[i];
                if (t == null)
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition at index {i} is null"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(t.FromNodeGuid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition at index {i} has empty FromNodeGuid",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }
                else if (!guidToNode.ContainsKey(t.FromNodeGuid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition at index {i} references missing FromNodeGuid '{t.FromNodeGuid}'",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }

                if (string.IsNullOrWhiteSpace(t.ToNodeGuid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition at index {i} has empty ToNodeGuid",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }
                else if (!guidToNode.ContainsKey(t.ToNodeGuid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition at index {i} references missing ToNodeGuid '{t.ToNodeGuid}'",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }

                // BLOCKING: eventName must not be empty
                if (string.IsNullOrWhiteSpace(t.EventName))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition at index {i} has empty EventName (blocking error)",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }

                // NOTE: Condition == null is acceptable (treated as always true at runtime).
            }

            // --- Ambiguity warnings: multiple transitions share the same (from,event) ---
            // Runtime rule: first true wins by order => warn if there are 2+ candidates.
            var grouped = transitions
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.FromNodeGuid) && !string.IsNullOrWhiteSpace(t.EventName))
                .GroupBy(t => (From: t.FromNodeGuid, Event: t.EventName));

            foreach (var g in grouped)
            {
                // It becomes *really* ambiguous when there are multiple candidates with null condition (always-true).
                if (g.Count() <= 1) continue;

                issues.Add(new ScreenFlowValidationIssue(
                    ScreenFlowValidationSeverity.Warning,
                    $"Multiple transitions share the same (FromNodeGuid, EventName). Runtime resolves by order using first condition-true. Count={g.Count()}.",
                    fromNodeGuid: g.Key.From,
                    eventName: g.Key.Event));
            }

            return issues;
        }

        public static bool HasErrors(IReadOnlyList<ScreenFlowValidationIssue> issues)
        {
            if (issues == null) return true;
            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ScreenFlowValidationSeverity.Error) return true;
            }

            return false;
        }
    }
}