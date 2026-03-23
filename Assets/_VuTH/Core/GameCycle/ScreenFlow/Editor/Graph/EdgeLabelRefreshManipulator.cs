using System;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph{
    /// <summary>
    /// Manipulator that tracks when a node is being dragged and periodically
    /// refreshes edge label positions during the drag.
    /// This is the correct UIToolkit pattern for handling drag-related updates.
    /// </summary>
    internal sealed class EdgeLabelRefreshManipulator : Manipulator
    {
        private readonly Action _refreshEdgeLabels;
        private bool _isDragging;

        public EdgeLabelRefreshManipulator(Action refreshEdgeLabels)
        {
            _refreshEdgeLabels = refreshEdgeLabels ?? throw new ArgumentNullException(nameof(refreshEdgeLabels));
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
            CancelRefresh();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            // Start of any potential drag operation (selection drag, node drag, etc.)
            StartRefresh();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            // Continue refreshing while mouse is moving (during drag)
            if (_isDragging)
            {
                _refreshEdgeLabels?.Invoke();
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            // End of drag operation
            CancelRefresh();
        }

        private void StartRefresh()
        {
            if (_isDragging) return;
            _isDragging = true;

            // Refresh immediately on drag start
            _refreshEdgeLabels?.Invoke();

            // Schedule periodic refresh during drag
            target.schedule.Execute(() =>
            {
                if (_isDragging)
                {
                    _refreshEdgeLabels?.Invoke();
                }
            }).Every(16); // ~60fps
        }

        private void CancelRefresh()
        {
            if (!_isDragging) return;
            _isDragging = false;
            target.schedule.Execute(() => { }).Until(() => !_isDragging);
        }
    }
}
