using Core.GameCycle.ScreenFlow.Condition;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.GameCycle.ScreenFlow.Editor.Graph
{
    internal sealed class ScreenTransitionLabel : VisualElement
    {
        private readonly Label label;

        public ScreenTransitionLabel(ScreenFlowTransition transition)
        {
            // Make sure clicks always go to the edge.
            pickingMode = PickingMode.Ignore;

            style.position = Position.Absolute;
            style.top = -18;
            style.left = 0;

            label = new Label(BuildText(transition));
            label.pickingMode = PickingMode.Ignore;
            label.focusable = false;

            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingLeft = 6;
            label.style.paddingRight = 6;
            label.style.paddingTop = 2;
            label.style.paddingBottom = 2;
            label.style.backgroundColor = new Color(0, 0, 0, 0.4f);
            label.style.color = Color.white;
            label.style.borderBottomLeftRadius = 4;
            label.style.borderBottomRightRadius = 4;
            label.style.borderTopLeftRadius = 4;
            label.style.borderTopRightRadius = 4;
            Add(label);

            // edge color hint is handled where edge is created; this is just text.
        }

        private static string BuildText(ScreenFlowTransition t)
        {
            var eventName = string.IsNullOrWhiteSpace(t.EventName) ? "<event>" : t.EventName;
            if (t.Condition == null)
                return eventName;

            return eventName + "\n[" + t.Condition.name + "]";
        }
    }
}
