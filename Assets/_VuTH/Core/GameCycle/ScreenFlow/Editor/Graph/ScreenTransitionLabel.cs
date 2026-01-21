using Core.GameCycle.ScreenFlow.Condition;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.GameCycle.ScreenFlow.Editor.Graph
{
    internal sealed class ScreenTransitionLabel : VisualElement
    {
        public ScreenTransitionLabel(ScreenFlowTransition transition)
        {
            // Make sure clicks always go to the edge.
            pickingMode = PickingMode.Ignore;

            style.position = Position.Absolute;
            style.top = -18;
            style.left = 0;

            var label = new Label(BuildText(transition))
            {
                pickingMode = PickingMode.Ignore,
                focusable = false,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    backgroundColor = new Color(0, 0, 0, 0.4f),
                    color = Color.white,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4
                }
            };

            Add(label);

            // edge color hint is handled where edge is created; this is just text.
        }

        private static string BuildText(ScreenFlowTransition t)
        {
            var eventName = string.IsNullOrWhiteSpace(t.EventName) ? "<event>" : t.EventName;
            if (!t.Condition)
                return eventName;

            return eventName + "\n[" + t.Condition.name + "]";
        }
    }
}
