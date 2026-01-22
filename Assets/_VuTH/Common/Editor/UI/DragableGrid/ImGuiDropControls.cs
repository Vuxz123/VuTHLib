using Common.Editor.UI.PreviewObject;
using UnityEditor;
using UnityEngine;

namespace Common.Editor.UI.DragableGrid
{
    public static class ImGuiDropControls
    {
        const float OptionBtnSize = 18f;

        // Helper to draw a lightweight object preview (icon + name) without consuming input events.

        public static ImGuiDropResult<T> ObjectDropSlot<T>(string label, T currentValue, bool allowSceneObjects = true,
            string requiredTag = null) where T : Object
        {
            Rect rect = EditorGUILayout.GetControlRect();
            return ObjectDropSlot(rect, new GUIContent(label), currentValue, allowSceneObjects, requiredTag);
        }

        public static ImGuiDropResult<T> ObjectDropSlot<T>(Rect rect, GUIContent label, T currentValue,
            bool allowSceneObjects, string requiredTag, System.Action<Rect, ImGuiDropResult<T>> customDrawer)
            where T : Object
        {
            Event e = Event.current;

            // Manually compute label + field rects so fieldRect always has a valid width
            float labelWidth = EditorGUIUtility.labelWidth;
            Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            Rect fieldRect = new Rect(labelRect.xMax, rect.y, Mathf.Max(1f, rect.width - labelWidth), rect.height);

            if (e.type == EventType.Repaint)
            {
                // Draw label and our custom field background/preview
                GUI.Label(labelRect, label, EditorStyles.label);
                PreviewObjectDrawer.DrawObjectPreview(fieldRect, currentValue);
            }

            // Entire rect participates in drop; highlight only the field area
            ImGuiDropResult<T> result = ImGuiDragAndDrop.HandleDropArea<T>(rect, fieldRect, requiredTag);

            customDrawer?.Invoke(fieldRect, result);

            return result;
        }

        // Overload without custom drawer keeps previous signature behavior
        public static ImGuiDropResult<T> ObjectDropSlot<T>(Rect rect, GUIContent label, T currentValue,
            bool allowSceneObjects = true, string requiredTag = null) where T : Object
        {
            return ObjectDropSlot(rect, label, currentValue, allowSceneObjects, requiredTag, null);
        }

        /// <summary>
        /// Layout-based drop zone helper. Allocates a rect via GUILayout and delegates to the Rect-based DropZone.
        /// </summary>
        public static ImGuiDropResult<T> DropZone<T>(float height, string label = null, string requiredTag = null,
            System.Action<Rect, ImGuiDropResult<T>> customDrawer = null) where T : Object
        {
            Rect rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            return DropZone(rect, label, requiredTag, customDrawer);
        }

        /// <summary>
        /// Generic drop zone that doesn't display a current value; it only reports drops.
        /// </summary>
        public static ImGuiDropResult<T> DropZone<T>(Rect rect, string label = null, string requiredTag = null,
            System.Action<Rect, ImGuiDropResult<T>> customDrawer = null) where T : Object
        {
            Rect contentRect = rect;
            if (!string.IsNullOrEmpty(label))
            {
                GUI.Label(rect, label, EditorStyles.boldLabel);
                contentRect = rect;
            }

            ImGuiDropResult<T> result = ImGuiDragAndDrop.HandleDropArea<T>(contentRect, contentRect, requiredTag);

            customDrawer?.Invoke(contentRect, result);

            return result;
        }

        /// <summary>
        /// Layout-based combined draggable + droppable zone.
        /// Behaves like an inventory slot: shows current value, can be dragged out, and accepts drops.
        /// Returns the potentially updated value and the drop result info.
        /// </summary>
        public static (T value, ImGuiDropResult<T> drop) DraggableDropZone<T>(
            string label,
            T currentValue,
            bool allowSceneObjects = true,
            string dragTag = null,
            string requiredTag = null,
            float height = 40f,
            System.Action<Rect, ImGuiDropResult<T>> customDrawer = null
        ) where T : Object
        {
            Rect rect = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
            return DraggableDropZone(rect, label, currentValue, allowSceneObjects, dragTag, requiredTag, customDrawer);
        }

        /// <summary>
        /// Rect-based combined draggable + droppable zone.
        /// </summary>
        public static (T value, ImGuiDropResult<T> drop) DraggableDropZone<T>(
            Rect rect,
            string label,
            T currentValue,
            bool allowSceneObjects = true,
            string dragTag = null,
            string requiredTag = null,
            System.Action<Rect, ImGuiDropResult<T>> customDrawer = null
        ) where T : Object
        {
            Event e = Event.current;

            // Layout: label on the left, content zone on the right
            Rect contentRect = rect;
            if (!string.IsNullOrEmpty(label))
            {
                float labelWidth = EditorGUIUtility.labelWidth;
                Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
                contentRect = new Rect(labelRect.xMax, rect.y, Mathf.Max(1f, rect.width - labelWidth), rect.height);

                if (e.type == EventType.Repaint)
                {
                    GUI.Label(labelRect, label, EditorStyles.boldLabel);
                }
            }

            // Pattern C logic
            bool tallEnoughForTwoSquares = contentRect.height >= 40f;

            Rect previewRect;
            Rect primaryBtnRect;
            Rect secondaryBtnRect = Rect.zero;

            float side = Mathf.Min(contentRect.height, OptionBtnSize); // square button size
            float spacing = 2f;

            if (tallEnoughForTwoSquares && contentRect.width > (side + spacing) * 2f)
            {
                // Two vertical square buttons on the right side
                primaryBtnRect = new Rect(
                    contentRect.xMax - side,
                    contentRect.y,
                    side,
                    side
                );

                secondaryBtnRect = new Rect(
                    contentRect.xMax - side,
                    contentRect.y + side + spacing,
                    side,
                    side
                );

                previewRect = new Rect(
                    contentRect.x,
                    contentRect.y,
                    Mathf.Max(1f, primaryBtnRect.xMin - spacing - contentRect.x),
                    contentRect.height
                );
            }
            else
            {
                // Not enough height: preview uses most of the rect, with a single dropdown button on the right
                primaryBtnRect = new Rect(
                    contentRect.xMax - side,
                    contentRect.y,
                    side,
                    contentRect.height
                );

                previewRect = new Rect(
                    contentRect.x,
                    contentRect.y,
                    Mathf.Max(1f, primaryBtnRect.xMin - spacing - contentRect.x),
                    contentRect.height
                );
            }

            // Draw preview in Repaint (adapts to previewRect size)
            if (e.type == EventType.Repaint)
            {
                PreviewObjectDrawer.DrawObjectPreview(previewRect, currentValue);
            }

            // Handle drag from the preview area
            ImGuiDragAndDrop.HandleDragSource(previewRect, currentValue, label, dragTag);

            // Handle drop over the preview area (same rect for hit test and highlight)
            ImGuiDropResult<T> dropResult = ImGuiDragAndDrop.HandleDropArea<T>(previewRect, previewRect, requiredTag);

            // Buttons behavior
            if (contentRect.height >= 20f)
            {
                if (tallEnoughForTwoSquares)
                {
                    // Top: picker, bottom: clear
                    int pickerControlId = GUIUtility.GetControlID(FocusType.Passive, primaryBtnRect);
                    if (GUI.Button(primaryBtnRect, "...", EditorStyles.miniButton))
                    {
                        EditorGUIUtility.ShowObjectPicker<T>(currentValue, allowSceneObjects, string.Empty,
                            pickerControlId);
                    }

                    if (GUI.Button(secondaryBtnRect, "x", EditorStyles.miniButton))
                    {
                        currentValue = null;
                    }

                    if ((e.commandName == "ObjectSelectorUpdated" || e.commandName == "ObjectSelectorClosed") &&
                        EditorGUIUtility.GetObjectPickerControlID() == pickerControlId)
                    {
                        var picked = EditorGUIUtility.GetObjectPickerObject() as T;
                        if (picked != null)
                        {
                            currentValue = picked;
                        }
                    }
                }
                else
                {
                    // Single dropdown button: open a context menu for actions
                    if (GUI.Button(primaryBtnRect, "⋮", EditorStyles.miniButton))
                    {
                        GenericMenu menu = new GenericMenu();
                        // Avoid capturing mutable locals (currentValue) directly in the closure; capture a snapshot instead.
                        var capturedCurrent = currentValue;
                        var capturedAllowScene = allowSceneObjects;
                        menu.AddItem(new GUIContent("Pick Object"), false, () =>
                        {
                            int pickerId = GUIUtility.GetControlID(FocusType.Passive);
                            EditorGUIUtility.ShowObjectPicker<T>(capturedCurrent, capturedAllowScene, string.Empty,
                                pickerId);
                        });

                        if (currentValue != null)
                        {
                            menu.AddItem(new GUIContent("Clear"), false, () => { currentValue = null; });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("Clear"));
                        }

                        menu.DropDown(primaryBtnRect);
                    }
                }
            }

            // Custom visuals on top of preview area
            customDrawer?.Invoke(previewRect, dropResult);

            if (dropResult.Performed && dropResult.DroppedObject != null)
            {
                currentValue = dropResult.DroppedObject;
            }

            return (currentValue, dropResult);
        }
    }
}
