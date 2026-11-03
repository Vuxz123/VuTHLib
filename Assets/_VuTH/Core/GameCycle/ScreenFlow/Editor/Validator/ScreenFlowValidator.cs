using System;
using System.Collections.Generic;
using System.Linq;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Validator
{
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

            string NodeName(string guid) =>
                guid != null && guidToNode.TryGetValue(guid, out var n) && n.Screen ? n.Screen.name : $"<{guid}>";
            string Describe(ScreenFlowTransition tr) =>
                $"'{NodeName(tr.FromNodeGuid)} --[{tr.EventName}]--> {NodeName(tr.ToNodeGuid)}'";

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
                        $"Transition {Describe(t)} has empty FromNodeGuid",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }
                else if (!guidToNode.ContainsKey(t.FromNodeGuid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition {Describe(t)} references missing FromNodeGuid '{t.FromNodeGuid}'",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }

                if (string.IsNullOrWhiteSpace(t.ToNodeGuid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition {Describe(t)} has empty ToNodeGuid",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }
                else if (!guidToNode.ContainsKey(t.ToNodeGuid))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition {Describe(t)} references missing ToNodeGuid '{t.ToNodeGuid}'",
                        fromNodeGuid: t.FromNodeGuid,
                        toNodeGuid: t.ToNodeGuid,
                        eventName: t.EventName));
                }

                // BLOCKING: eventName must not be empty
                if (string.IsNullOrWhiteSpace(t.EventName))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Error,
                        $"Transition {Describe(t)} has empty EventName (blocking error)",
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

            // --- Event names ---
            var eventNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in transitions)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.EventName)) continue;
                eventNames.Add(t.EventName);

                if (t.EventName != t.EventName.Trim())
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Warning,
                        $"Event name '{t.EventName}' has leading/trailing spaces; Trigger() matches names exactly.",
                        fromNodeGuid: t.FromNodeGuid, toNodeGuid: t.ToNodeGuid, eventName: t.EventName));
                }
            }

            // Different names that become the same generated constant (e.g. "open shop" / "open_shop").
            foreach (var g in eventNames.GroupBy(Codegen.ScreenFlowEventsGenerator.ToIdentifier).Where(g => g.Count() > 1))
            {
                issues.Add(new ScreenFlowValidationIssue(
                    ScreenFlowValidationSeverity.Warning,
                    $"Events {string.Join(", ", g.Select(e => $"'{e}'"))} all map to ScreenFlowEvents.{g.Key}; " +
                    "the generated constants get numeric suffixes. Consider unifying the names."));
            }

            // --- Navigation modes ---
            var incomingModes = new Dictionary<string, HashSet<ScreenFlowNavigation>>(StringComparer.Ordinal);
            foreach (var t in transitions)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.ToNodeGuid)) continue;
                if (!incomingModes.TryGetValue(t.ToNodeGuid, out var modes))
                {
                    modes = new HashSet<ScreenFlowNavigation>();
                    incomingModes[t.ToNodeGuid] = modes;
                }
                modes.Add(t.Navigation);
            }

            foreach (var t in transitions)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.FromNodeGuid)) continue;

                var required = t.Navigation switch
                {
                    ScreenFlowNavigation.Pop => ScreenFlowNavigation.Push,
                    ScreenFlowNavigation.PopOverride => ScreenFlowNavigation.PushOverride,
                    _ => (ScreenFlowNavigation?)null
                };
                if (required == null) continue;

                var reachedBy = incomingModes.TryGetValue(t.FromNodeGuid, out var m) ? m : null;
                if (reachedBy == null || !reachedBy.Contains(required.Value))
                {
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Warning,
                        $"Transition {Describe(t)} uses {t.Navigation}, but '{NodeName(t.FromNodeGuid)}' is never " +
                        $"entered with {required}. At runtime it falls back to Enter.",
                        fromNodeGuid: t.FromNodeGuid, toNodeGuid: t.ToNodeGuid, eventName: t.EventName));
                }
            }

            // --- Reachability from the start node (terminal nodes without outgoing transitions are fine) ---
            if (!string.IsNullOrWhiteSpace(graph.StartNodeGuid) && guidToNode.ContainsKey(graph.StartNodeGuid))
            {
                var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var t in transitions)
                {
                    if (t == null || string.IsNullOrWhiteSpace(t.FromNodeGuid) || string.IsNullOrWhiteSpace(t.ToNodeGuid)) continue;
                    if (!outgoing.TryGetValue(t.FromNodeGuid, out var list))
                    {
                        list = new List<string>();
                        outgoing[t.FromNodeGuid] = list;
                    }
                    list.Add(t.ToNodeGuid);
                }

                var reachable = new HashSet<string>(StringComparer.Ordinal) { graph.StartNodeGuid };
                var queue = new Queue<string>();
                queue.Enqueue(graph.StartNodeGuid);
                while (queue.Count > 0)
                {
                    if (!outgoing.TryGetValue(queue.Dequeue(), out var next)) continue;
                    foreach (var guid in next)
                    {
                        if (reachable.Add(guid)) queue.Enqueue(guid);
                    }
                }

                foreach (var node in guidToNode.Values)
                {
                    if (reachable.Contains(node.Guid)) continue;
                    issues.Add(new ScreenFlowValidationIssue(
                        ScreenFlowValidationSeverity.Warning,
                        $"Node '{NodeName(node.Guid)}' cannot be reached from the start node.",
                        nodeGuid: node.Guid));
                }
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