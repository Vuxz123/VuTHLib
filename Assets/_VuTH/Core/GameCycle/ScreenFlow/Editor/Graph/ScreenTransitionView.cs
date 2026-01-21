using Core.GameCycle.ScreenFlow.Condition;

namespace Core.GameCycle.ScreenFlow.Editor.Graph
{
    public class ScreenTransitionView
    {
        public string FromGuid;
        public string ToGuid;
        public string EventName;
        public TransitionCondition Condition;
    }
}