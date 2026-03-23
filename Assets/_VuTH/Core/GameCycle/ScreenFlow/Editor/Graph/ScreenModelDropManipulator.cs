using System;
using System.Collections.Generic;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Core.A;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    public sealed class ScreenModelDropManipulator : Manipulator
    {
        private readonly Func<List<ScreenModel>> _getDraggedScreenModels;
        private readonly Func<Vector2, Vector2> _worldToLocal;
        private readonly Action<Vector2, List<ScreenModel>> _onDrop;

        public ScreenModelDropManipulator(
            Func<List<ScreenModel>> getDraggedScreenModels,
            Func<Vector2, Vector2> worldToLocal,
            Action<Vector2, List<ScreenModel>> onDrop)
        {
            _getDraggedScreenModels = getDraggedScreenModels ?? throw new ArgumentNullException(nameof(getDraggedScreenModels));
            _worldToLocal = worldToLocal ?? throw new ArgumentNullException(nameof(worldToLocal));
            _onDrop = onDrop ?? throw new ArgumentNullException(nameof(onDrop));
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            target.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
            target.UnregisterCallback<DragPerformEvent>(OnDragPerform);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            var screens = _getDraggedScreenModels();
            if (screens.Count == 0) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var screens = _getDraggedScreenModels();
            if (screens.Count == 0) return;

            DragAndDrop.AcceptDrag();
            var localPosition = _worldToLocal(evt.mousePosition);
            _onDrop(localPosition, screens);
            evt.StopPropagation();
        }
    }
}
