using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    internal sealed class ScreenTransitionLabel : VisualElement
    {
        private readonly Edge _edge;
        private readonly Label _label;

        public ScreenTransitionLabel(Edge edge, ScreenFlowTransition transition)
        {
            _edge = edge;

            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;

            _label = new Label(BuildText(transition))
            {
                pickingMode = PickingMode.Ignore,
                focusable = false
            };

            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.paddingLeft = 6;
            _label.style.paddingRight = 6;
            _label.style.paddingTop = 2;
            _label.style.paddingBottom = 2;
            _label.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            _label.style.color = Color.white;
            _label.style.borderBottomLeftRadius = 4;
            _label.style.borderBottomRightRadius = 4;
            _label.style.borderTopLeftRadius = 4;
            _label.style.borderTopRightRadius = 4;

            Add(_label);

            RegisterCallback<AttachToPanelEvent>(_ => StartTracking());
            RegisterCallback<DetachFromPanelEvent>(_ => StopTracking());
        }

        private void StartTracking()
        {
            if (_edge.output != null)
            {
                _edge.output.RegisterCallback<GeometryChangedEvent>(OnPortGeometryChanged);
            }
            UpdatePosition();
        }

        private void StopTracking()
        {
            if (_edge.output != null)
            {
                _edge.output.UnregisterCallback<GeometryChangedEvent>(OnPortGeometryChanged);
            }
        }

        private void OnPortGeometryChanged(GeometryChangedEvent evt)
        {
            UpdatePosition();
        }

        internal void UpdatePosition()
        {
            if (_edge.output == null || _edge.input == null) return;

            var fromWorld = _edge.output.worldBound.center;
            var toWorld = _edge.input.worldBound.center;
            var midpointWorld = (fromWorld + toWorld) * 0.5f;
            var midpointLocal = _edge.WorldToLocal(midpointWorld);

            var width = _label.resolvedStyle.width;
            var height = _label.resolvedStyle.height;

            if (float.IsNaN(width) || width <= 0f) width = 80f;
            if (float.IsNaN(height) || height <= 0f) height = 20f;

            style.left = midpointLocal.x - (width * 0.5f);
            style.top = midpointLocal.y - height - 6f;
        }

        private static string BuildText(ScreenFlowTransition transition)
        {
            var eventName = string.IsNullOrWhiteSpace(transition.EventName) ? "<event>" : transition.EventName;
            if (!transition.Condition)
            {
                return eventName;
            }

            return eventName + "\n[" + transition.Condition.name + "]";
        }
    }
}
