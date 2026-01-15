using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Core.GameCycle.ScreenFlow.Editor.Graph
{
    public class ScreenFlowEdgeManipulator : EdgeManipulator
    {
        protected override void RegisterCallbacksOnTarget()
        {
            base.RegisterCallbacksOnTarget();
            this.target.RegisterCallback<PointerDownEvent>(OnScreenFlowEdgePointerDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            base.UnregisterCallbacksFromTarget();
            this.target.RegisterCallback<PointerDownEvent>(OnScreenFlowEdgePointerDown);
        }

        private void OnScreenFlowEdgePointerDown(PointerDownEvent evt)
        {
            
        }
    }
}