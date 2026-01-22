using UnityEditor;
using UnityEngine;

namespace Common.Editor.UI.DragableGrid
{
    public static class ImGuiDraggableControls
    {
        public static T DraggableObjectField<T>(string label, T value, bool allowSceneObjects = true, string dragTag = null) where T : Object
        {
            Rect rect = EditorGUILayout.GetControlRect();
            return DraggableObjectField(rect, new GUIContent(label), value, allowSceneObjects, dragTag);
        }

        public static T DraggableObjectField<T>(Rect rect, GUIContent label, T value, bool allowSceneObjects = true, string dragTag = null) where T : Object
        {
            Rect fieldRect = EditorGUI.PrefixLabel(rect, label);
            EditorGUI.BeginChangeCheck();
            T newValue = (T)EditorGUI.ObjectField(fieldRect, GUIContent.none, value, typeof(T), allowSceneObjects);
            if (EditorGUI.EndChangeCheck())
            {
                value = newValue;
            }

            // Make the whole field a drag source for the current value
            ImGuiDragAndDrop.HandleDragSource(rect, value, label?.text, dragTag);

            return value;
        }
    }
}