using System;
using System.Collections.Generic;
using System.IO;
using _VuTH.Common.Scene;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Scene
{
    /// <summary>
    /// Drawer for <see cref="SceneSelectorAttribute"/>: a popup of the enabled Build Settings scenes (plus "- None -").
    /// The scene list is cached and rebuilt when Build Settings change. A stored name that is no longer in Build
    /// Settings is shown as a red "(Missing!)" button that clears the value.
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneSelectorAttribute))]
    public sealed class SceneSelectorDrawer : PropertyDrawer
    {
        private const string NoneOption = "- None -";

        // GUIContent is cheaper than string when the popup is drawn many times.
        private static GUIContent[] _displayOptions;
        private static string[] _sceneValues;
        private static bool _isListenerRegistered;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [SceneSelector] with strings.");
                return;
            }

            EnsureSceneList();

            var currentName = property.stringValue;
            var currentIndex = Array.IndexOf(_sceneValues, currentName);

            if (currentIndex < 0)
            {
                if (!string.IsNullOrEmpty(currentName))
                {
                    DrawMissingScene(position, property, label, currentName);
                    return;
                }

                currentIndex = 0;
            }

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUI.Popup(position, label, currentIndex, _displayOptions);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = _sceneValues[newIndex];
            }
        }

        private static void EnsureSceneList()
        {
            if (!_isListenerRegistered)
            {
                EditorBuildSettings.sceneListChanged += InvalidateSceneList;
                _isListenerRegistered = true;
            }

            if (_displayOptions == null)
            {
                BuildSceneList();
            }
        }

        private static void BuildSceneList()
        {
            var options = new List<GUIContent> { new(NoneOption) };
            var values = new List<string> { string.Empty };

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;

                var name = Path.GetFileNameWithoutExtension(scene.path);
                options.Add(new GUIContent(name));
                values.Add(name);
            }

            _displayOptions = options.ToArray();
            _sceneValues = values.ToArray();
        }

        private static void InvalidateSceneList()
        {
            _displayOptions = null;
            _sceneValues = null;
        }

        private static void DrawMissingScene(Rect position, SerializedProperty property, GUIContent label, string currentName)
        {
            var labelWidth = EditorGUIUtility.labelWidth;
            var labelRect = new Rect(position.x, position.y, labelWidth, position.height);
            var contentRect = new Rect(position.x + labelWidth, position.y, position.width - labelWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);

            if (GUI.Button(contentRect, new GUIContent($"{currentName} (Missing!)", "Not in Build Settings. Click to clear.")))
            {
                property.stringValue = string.Empty;
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = originalColor;
        }
    }
}
