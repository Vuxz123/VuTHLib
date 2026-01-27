using _VuTH.Core.GameCycle.ScreenFlow.Condition;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    public class ScreenTransitionView
    {
        public string FromGuid;
        public string ToGuid;
        public string EventName;
        public TransitionCondition Condition;
    }
}