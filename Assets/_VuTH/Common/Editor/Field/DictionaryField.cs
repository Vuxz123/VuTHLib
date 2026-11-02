using System;
using System.Collections.Generic;
using _VuTH.Common.Editor.Helpers;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Helper API to draw a SerializableDictionary property from custom inspectors.
    /// Usage: DictionaryField.Draw(property, label);
    /// </summary>
    /// <remarks>
    /// This class is the IMGUI view/coordinator. Collaborators:
    /// <see cref="DictionaryViewStateStore"/> (state + EditorPrefs persistence),
    /// <see cref="SerializedDictionaryOperations"/> (array operations, filtering, duplicate policy),
    /// <see cref="DictionaryRowInteraction"/> (selection and drag reorder),
    /// <see cref="DictionaryJsonCodec"/> (JSON import/export).
    /// </remarks>
    public static class DictionaryField
    {
        public static void Draw(SerializedProperty dictProperty, GUIContent label)
        {
            if (dictProperty == null)
            {
                EditorGUILayout.LabelField(label ?? GUIContent.none, new GUIContent("(null)"));
                return;
            }

            // Outer container box for the whole dictionary field
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Resolve backing arrays first so we can show count in header
            var keysProp = dictProperty.FindPropertyRelative(SerializableDictionary<object, object>.KeysFieldName) ??
                           dictProperty.FindPropertyRelative("keys");
            var valuesProp =
                dictProperty.FindPropertyRelative(SerializableDictionary<object, object>.ValuesFieldName) ??
                dictProperty.FindPropertyRelative("values");
            var itemCount = keysProp?.arraySize ?? 0;

            var headerText = (label != null && !string.IsNullOrEmpty(label.text))
                ? label.text
                : dictProperty.displayName;
            // Refactored: capture header expanded state in temp variable before assigning
            var newExpanded = CustomEditorStyles.DrawFoldoutHeader(headerText, dictProperty.isExpanded, EditorStyles.toolbar, itemCount);
            dictProperty.isExpanded = newExpanded;

            if (keysProp == null || valuesProp == null)
            {
                EditorGUILayout.HelpBox(
                    "Dictionary backing lists not found. Ensure you're using SerializableDictionary<TKey, TValue> and fields are named 'keys' and 'values'.",
                    MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            if (!dictProperty.isExpanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            // Content area
            EditorGUI.indentLevel++;

            if (valuesProp.arraySize != keysProp.arraySize)
                valuesProp.arraySize = keysProp.arraySize;

            var state = DictionaryViewStateStore.GetState(dictProperty);
            DictionaryViewStateStore.LoadState(dictProperty, state);

            // Header context menu (JSON export/import)
            Rect headerRect = GUILayoutUtility.GetLastRect();
            HandleHeaderContextMenu(headerRect, keysProp, valuesProp);

            // Toolbar: Search, Sort, Duplicate policy + selection actions
            DrawToolbar(dictProperty, state, keysProp, valuesProp);

            // Build visible indices using search filter
            var visible = SerializedDictionaryOperations.BuildVisibleIndices(keysProp, valuesProp, state.Search);

            var removeIndex = DrawRows(visible, state, keysProp, valuesProp);

            // Precise drag insertion index update (only when no search filter to avoid ambiguous reordering of filtered subset)
            DictionaryRowInteraction.UpdatePreciseInsertIndex(state, keysProp.arraySize);

            // Draw drag insertion line feedback
            DictionaryRowInteraction.DrawInsertionLine(state, headerRect);

            // Add row buttons
            DrawAddButtons(keysProp, valuesProp);

            // Apply single-row removal if requested
            if (removeIndex >= 0 && removeIndex < keysProp.arraySize)
            {
                SerializedDictionaryOperations.DeleteRowAt(keysProp, valuesProp, removeIndex);
            }

            EditorGUI.indentLevel--;

            // end outer box
            EditorGUILayout.EndVertical();
        }

        private static void DrawToolbar(SerializedProperty dictProperty, DictionaryViewState state,
            SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search:", GUILayout.Width(50));
            var prevSearch = state.Search;
            state.Search = GUILayout.TextField(state.Search ?? string.Empty, GUILayout.MinWidth(120));
            if (state.Search != prevSearch)
            {
                DictionaryViewStateStore.SaveState(dictProperty, state);
            }
            if (!string.IsNullOrEmpty(state.Search))
            {
                if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    state.Search = string.Empty;
                    DictionaryViewStateStore.SaveState(dictProperty, state);
                    GUI.FocusControl(null); // clear focus
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("A→Z", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                SerializedDictionaryOperations.SortByKey(keysProp, valuesProp, false);
            }

            if (GUILayout.Button("Z→A", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                SerializedDictionaryOperations.SortByKey(keysProp, valuesProp, true);
            }

            GUILayout.Space(8);
            bool prevPrevent = state.PreventDuplicates;
            var newPrevent = GUILayout.Toggle(state.PreventDuplicates, "Prevent duplicates", EditorStyles.toolbarButton);
            if (newPrevent != prevPrevent)
            {
                state.PreventDuplicates = newPrevent;
                DictionaryViewStateStore.SaveState(dictProperty, state);
            }
            if (state.PreventDuplicates)
            {
                var prevMode = state.DedupMode;
                state.DedupMode = (DictionaryDedupMode)EditorGUILayout.EnumPopup(state.DedupMode, GUILayout.Width(110));
                if (state.DedupMode != prevMode)
                {
                    DictionaryViewStateStore.SaveState(dictProperty, state);
                }
                SerializedDictionaryOperations.ApplyDuplicatePolicy(keysProp, valuesProp, state.DedupMode);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("Select All", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                state.Selected.Clear();
                for (int i = 0; i < keysProp.arraySize; i++) state.Selected.Add(i);
            }
            if (GUILayout.Button("Clear Sel", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                state.Selected.Clear();
            }
            if (state.Selected.Count > 0 && GUILayout.Button("Delete Sel", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                SerializedDictionaryOperations.DeleteSelected(keysProp, valuesProp, state.Selected);
                state.Selected.Clear();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Draws the visible rows and returns the row index whose "-" button was clicked (or -1).</summary>
        private static int DrawRows(List<int> visible, DictionaryViewState state,
            SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            var removeIndex = -1;
            var seen = new HashSet<string>();

            foreach (var i in visible)
            {
                // Row begin
                Rect rowStart = EditorGUILayout.BeginHorizontal();
                // Store rect & mapping later (after layout finalize on EndHorizontal) - we push preliminary rect now
                // We'll capture after EndHorizontal using GUILayoutUtility.GetLastRect()
                bool isSelected = state.Selected.Contains(i);
                // Highlight selected row background
                if (Event.current.type == EventType.Repaint && isSelected)
                {
                    var highlightRect = rowStart;
                    highlightRect.x = 0; highlightRect.width = EditorGUIUtility.currentViewWidth;
                    EditorGUI.DrawRect(highlightRect, new Color(0.25f, 0.55f, 0.95f, 0.25f));
                }

                // Drag handle
                GUILayout.Label("≡", GUILayout.Width(16));

                // Selection toggle (click on index label while modifiers)
                var indexLabelRect = GUILayoutUtility.GetRect(new GUIContent($"[{i}]"), GUI.skin.label, GUILayout.Width(40));
                GUI.Label(indexLabelRect, $"[{i}]");

                DictionaryRowInteraction.HandleRowSelection(indexLabelRect, i, state);
                DictionaryRowInteraction.HandleRowDrag(rowStart, i, state, keysProp, valuesProp);

                var keyProp = keysProp.GetArrayElementAtIndex(i);
                var valueProp = valuesProp.GetArrayElementAtIndex(i);

                EditorGUILayout.PropertyField(keyProp, GUIContent.none, true, GUILayout.MinWidth(50));
                EditorGUILayout.PropertyField(valueProp, GUIContent.none, true, GUILayout.MinWidth(50));

                if (GUILayout.Button("⋮", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    ShowRowContextMenu(i, keysProp, valuesProp, r => removeIndex = r);
                }

                if (GUILayout.Button("-", GUILayout.Width(22)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();

                // Capture final row rect after layout
                var finalRowRect = GUILayoutUtility.GetLastRect();
                state.CurrentRowRects.Add(finalRowRect);
                state.CurrentVisibleIndices.Add(i);

                // Context-click (right click) also triggers menu
                var lastRect = rowStart;
                var e = Event.current;
                if (e.type == EventType.ContextClick && lastRect.Contains(e.mousePosition))
                {
                    ShowRowContextMenu(i, keysProp, valuesProp, r => removeIndex = r);
                    e.Use();
                }

                var keyId = SerializedDictionaryOperations.PropertyKeyString(keyProp);
                if (!string.IsNullOrEmpty(keyId))
                {
                    if (!seen.Add(keyId))
                        EditorGUILayout.HelpBox("Duplicate key detected; only the last one will be used at runtime.",
                            MessageType.Warning);
                }
            }

            return removeIndex;
        }

        private static void DrawAddButtons(SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Empty", GUILayout.Width(90)))
            {
                SerializedDictionaryOperations.AddNewRow(keysProp, valuesProp, uniqueString: false);
            }
            if (SerializedDictionaryOperations.IsStringKey(keysProp) && GUILayout.Button("Add Template", GUILayout.Width(100)))
            {
                SerializedDictionaryOperations.AddNewRow(keysProp, valuesProp, uniqueString: true);
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void ShowRowContextMenu(int index,
            SerializedProperty keysProp,
            SerializedProperty valuesProp,
            Action<int> onRemove)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate"), false, () => SerializedDictionaryOperations.DuplicateRow(index, keysProp, valuesProp));
            if (index > 0)
                menu.AddItem(new GUIContent("Move Up"), false, () => SerializedDictionaryOperations.MoveRow(index, index - 1, keysProp, valuesProp));
            else
                menu.AddDisabledItem(new GUIContent("Move Up"));
            if (index < keysProp.arraySize - 1)
                menu.AddItem(new GUIContent("Move Down"), false, () => SerializedDictionaryOperations.MoveRow(index, index + 1, keysProp, valuesProp));
            else
                menu.AddDisabledItem(new GUIContent("Move Down"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, () => onRemove?.Invoke(index));
            menu.ShowAsContext();
        }

        private static void HandleHeaderContextMenu(Rect headerRect, SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            var e = Event.current;
            if (e.type == EventType.ContextClick && headerRect.Contains(e.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Export JSON"), false, () => DictionaryJsonCodec.ExportJson(keysProp, valuesProp));
                menu.AddItem(new GUIContent("Import JSON"), false, () => DictionaryJsonCodec.ImportJson(keysProp, valuesProp));
                menu.ShowAsContext();
                e.Use();
            }
        }
    }

    /// <summary>
    /// PropertyDrawer that enables drawing any field of type SerializableDictionary&lt;TKey, TValue&gt; with default inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    public class SerializableDictionaryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Use dynamic height by drawing via EditorGUI.PropertyField with includeChildren true.
            // We'll handle layout in OnGUI; return single line to allow GUILayout-driven height.
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // We will use GUILayout-based drawing to keep it simple and flexible.
            // Draw a foldout header and the list below.
            EditorGUI.BeginProperty(position, label, property);
            DictionaryField.Draw(property, label);
            EditorGUI.EndProperty();
        }
    }
}

