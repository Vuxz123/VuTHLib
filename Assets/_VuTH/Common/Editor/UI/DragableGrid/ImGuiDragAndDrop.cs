using UnityEditor;
using UnityEngine;

namespace Common.Editor.UI
{
    /// <summary>
    /// Simple IMGUI-based drag & drop helper for Editor-time usage.
    /// Lets you define draggable object fields and drop slots that work across windows.
    /// </summary>
    public static class ImGuiDragAndDrop
    {
        private const float DragStartThreshold = 5f;

        private static ImGuiDragPayload _currentPayload;
        private static Vector2 _dragStartMousePos;
        private static bool _dragging;

        public static ImGuiDragPayload CurrentPayload => _currentPayload;

        #region Drag source

        /// <summary>
        /// Handle drag events for a given rect. Call this with the exact rect you used to draw the control
        /// (for example, the field rect returned by EditorGUI.PrefixLabel or EditorGUILayout.GetControlRect).
        /// </summary>
        public static bool HandleDragSource(Rect rect, Object obj, string label = null, string tag = null)
        {
            Event e = Event.current;

            // Use Passive so we cooperate nicely with other GUI controls
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (rect.Contains(e.mousePosition) && e.button == 0)
                    {
                        GUIUtility.hotControl = controlId;
                        _dragStartMousePos = e.mousePosition;
                        _dragging = false;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId && e.button == 0)
                    {
                        if (!_dragging)
                        {
                            if ((e.mousePosition - _dragStartMousePos).sqrMagnitude >= DragStartThreshold * DragStartThreshold)
                            {
                                if (obj != null)
                                {
                                    BeginDrag(obj, label, tag, rect);
                                    _dragging = true;

                                    DragAndDrop.PrepareStartDrag();
                                    DragAndDrop.objectReferences = new[] { obj };
                                    DragAndDrop.StartDrag(label ?? obj.name);
                                }
                            }
                        }
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId && e.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        _dragging = false;
                        e.Use();
                    }
                    break;
            }

            return _dragging;
        }

        public static bool BeginDrag(Object obj, string label, string tag, Rect sourceRect)
        {
            if (obj == null)
            {
                return false;
            }

            _currentPayload = new ImGuiDragPayload(
                id: obj.GetEntityId(),
                obj: obj,
                type: obj.GetType(),
                label: label,
                tag: tag,
                sourceRect: GUIUtility.GUIToScreenRect(sourceRect)
            );

            return true;
        }

        public static void CancelDrag()
        {
            _currentPayload = null;
            _dragging = false;
        }

        #endregion

        #region Drop target helpers
        
        
        public static ImGuiDropResult<T> HandleDropArea<T>(Rect area, Rect highlightedArea, string requiredTag) where T : Object
        {
            ImGuiDropResult<T> result = new ImGuiDropResult<T>();

            int controlId = GUIUtility.GetControlID(FocusType.Passive, area);
            Event e = Event.current;

            result.IsHovering = area.Contains(e.mousePosition);
            bool canAccept = CanAccept(typeof(T), requiredTag);
            result.CanAccept = canAccept;

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (result.IsHovering && canAccept)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                        if (e.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();

                            if (EvaluateDropArea(area, typeof(T), requiredTag, out Object obj, out ImGuiDragPayload payload))
                            {
                                result.Performed = true;
                                result.DroppedObject = (T)obj;
                                result.Payload = payload;
                                CancelDrag();
                            }

                            e.Use();
                        }
                        else
                        {
                            e.Use();
                        }
                    }
                    break;

                case EventType.MouseUp:
                    if (result.IsHovering && canAccept && e.button == 0)
                    {
                        if (EvaluateDropArea(area, typeof(T), requiredTag, out Object obj, out ImGuiDragPayload payload))
                        {
                            result.Performed = true;
                            result.DroppedObject = (T)obj;
                            result.Payload = payload;
                            CancelDrag();
                            e.Use();
                        }
                    }
                    break;

                case EventType.Repaint:
                    if (result.IsHovering && canAccept && CurrentPayload != null)
                    {
                        DrawDropHighlight(highlightedArea, true);
                    }
                    break;
            }
            
            return result;
        }

        public static bool CanAccept<T>(string requiredTag = null) where T : Object
        {
            return CanAccept(typeof(T), requiredTag);
        }

        public static bool CanAccept(System.Type type, string requiredTag = null)
        {
            if (_currentPayload == null || _currentPayload.Object == null)
                return false;

            if (!type.IsAssignableFrom(_currentPayload.ObjectType))
                return false;

            if (!string.IsNullOrEmpty(requiredTag) && _currentPayload.Tag != requiredTag)
                return false;

            return true;
        }

        internal static bool EvaluateDropArea(Rect rect, System.Type expectedType, string requiredTag, out Object obj, out ImGuiDragPayload payload)
        {
            obj = null;
            payload = null;

            Event e = Event.current;
            bool isHovering = rect.Contains(e.mousePosition);

            if (_currentPayload == null || !isHovering)
                return false;

            if (!CanAccept(expectedType, requiredTag))
                return false;

            // Integrate with Unity's DragAndDrop if present
            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
            {
                Object candidate = DragAndDrop.objectReferences[0];
                if (candidate != null && expectedType.IsAssignableFrom(candidate.GetType()))
                {
                    obj = candidate;
                    payload = _currentPayload;
                    return true;
                }
            }

            // Fallback to our own payload
            obj = _currentPayload.Object;
            payload = _currentPayload;
            return true;
        }

        public static void DrawDropHighlight(Rect rect, bool valid)
        {
            Color col = valid ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.25f);
            EditorGUI.DrawRect(rect, col);
        }

        #endregion
    }
}
