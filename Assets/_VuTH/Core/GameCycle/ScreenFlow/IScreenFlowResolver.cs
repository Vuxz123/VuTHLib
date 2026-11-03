using System.Collections.Generic;

namespace _VuTH.Core.GameCycle.ScreenFlow
{
    public interface IScreenFlowResolver
    {
        ScreenFlowNode GetStartNode();

        bool TryResolve(ScreenFlowNode currentNode, string eventName, out ScreenFlowNode nextNode);

        /// <summary>Like <see cref="TryResolve"/>, but also returns the winning transition (navigation mode etc.).</summary>
        bool TryResolveTransition(ScreenFlowNode currentNode, string eventName,
            out ScreenFlowTransition transition, out ScreenFlowNode nextNode);

        /// <summary>True when any transition in the graph uses <paramref name="eventName"/> (typo detection).</summary>
        bool HasEvent(string eventName);

        IReadOnlyList<ScreenFlowTransition> GetAvailableTransitions(ScreenFlowNode fromNode, string eventName);
    }
}