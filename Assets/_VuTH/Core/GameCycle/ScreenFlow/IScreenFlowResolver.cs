using System.Collections.Generic;

namespace Core.GameCycle.ScreenFlow
{
    public interface IScreenFlowResolver
    {
        ScreenFlowNode GetStartNode();

        bool TryResolve(ScreenFlowNode currentNode, string eventName, out ScreenFlowNode nextNode);

        IReadOnlyList<ScreenFlowTransition> GetAvailableTransitions(ScreenFlowNode fromNode, string eventName);
    }
}