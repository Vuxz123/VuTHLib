using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Draws a <see cref="SerializableDictionary{TKey,TValue}"/> property as a <see cref="ReorderableList"/>:
    /// <list type="bullet">
    /// <item>header row: foldout with item count, search, sort A→Z / Z→A, options menu (duplicate-key policy, JSON import/export);</item>
    /// <item>rows: key | value, drag to reorder (disabled while searching), right-click for Duplicate / Move / Delete;</item>
    /// <item>duplicate keys are flagged per row with which entry wins at runtime (the last one).</item>
    /// </list>
    /// Rect-based, so it reports its real height to the inspector and works inside arrays and other drawers.
    /// For custom inspectors use <see cref="Draw(SerializedProperty, GUIContent)"/> (layout) or
    /// <see cref="GetHeight"/> + <see cref="Draw(Rect, SerializedProperty, GUIContent)"/>.
    /// </summary>
    public static class DictionaryField
    {
        private const float KeyColumnRatio = 0.4f;
        private const float ColumnGap = 6f;
        private const float RowPadding = 2f;
        private const float IconWidth = 18f;
        private const float SearchWidth = 140f;
        private const float SortButtonWidth = 34f;
        private const float MenuButtonWidth = 22f;
        private const float DragHandleWidth = 14f;

        private static float Line => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;
        private static float HelpBoxHeight => Line * 2f + 4f;

        private static GUIContent _warnIcon;
        private static GUIContent WarnIcon => _warnIcon ??= EditorGUIUtility.IconContent("console.warnicon.sml");

        #region Public API

        /// <summary>Layout version for custom inspectors.</summary>
        public static void Draw(SerializedProperty dictProperty, GUIContent label)
        {
            if (dictProperty == null)
            {
                EditorGUILayout.LabelField(label ?? GUIContent.none, new GUIContent("(null)"));
                return;
            }

            var rect = EditorGUILayout.GetControlRect(true, GetHeight(dictProperty, label));
            Draw(rect, dictProperty, label);
        }

        /// <summary>Total height needed by <see cref="Draw(Rect, SerializedProperty, GUIContent)"/>.</summary>
        public static float GetHeight(SerializedProperty dictProperty, GUIContent label)
        {
            if (!TryResolve(dictProperty, out var keys, out var values)) return Line + Spacing + HelpBoxHeight;
            if (!dictProperty.isExpanded) return Line;

            var state = Prepare(dictProperty, keys, values);
            return Line + Spacing + state.List.GetHeight();
        }

        /// <summary>Rect version, used by the property drawers.</summary>
        public static void Draw(Rect position, SerializedProperty dictProperty, GUIContent label)
        {
            var title = label != null && !string.IsNullOrEmpty(label.text) ? label.text : dictProperty.displayName;
            var headerRect = new Rect(position.x, position.y, position.width, Line);

            if (!TryResolve(dictProperty, out var keys, out var values))
            {
                EditorGUI.LabelField(headerRect, title);
                var box = new Rect(position.x, headerRect.yMax + Spacing, position.width, HelpBoxHeight);
                EditorGUI.HelpBox(box,
                    "Dictionary backing lists not found. Use SerializableDictionary<TKey, TValue> (fields 'keys' and 'values').",
                    MessageType.Error);
                return;
            }

            if (values.arraySize != keys.arraySize) values.arraySize = keys.arraySize;

            var state = Prepare(dictProperty, keys, values);
            DrawHeader(headerRect, dictProperty, title, state);
            if (!dictProperty.isExpanded) return;

            if (state.PreventDuplicates)
            {
                SerializedDictionaryOperations.ApplyDuplicatePolicy(keys, values, state.DedupMode);
                state = Prepare(dictProperty, keys, values);
            }

            var indent = EditorGUI.indentLevel;
            var listRect = EditorGUI.IndentedRect(
                new Rect(position.x, headerRect.yMax + Spacing, position.width, state.List.GetHeight()));
            EditorGUI.indentLevel = 0;
            try
            {
                state.List.DoList(listRect);
            }
            finally
            {
                EditorGUI.indentLevel = indent;
                state.Keys = null;
                state.Values = null;
            }
        }

        #endregion

        #region State

        private static bool TryResolve(SerializedProperty dictProperty, out SerializedProperty keys, out SerializedProperty values)
        {
            keys = dictProperty?.FindPropertyRelative(SerializableDictionary<object, object>.KeysFieldName);
            values = dictProperty?.FindPropertyRelative(SerializableDictionary<object, object>.ValuesFieldName);
            return keys != null && values != null && keys.isArray && values.isArray;
        }

        /// <summary>Binds the current GUI pass's properties and rebuilds visible rows + duplicate info.</summary>
        private static DictionaryViewState Prepare(SerializedProperty dictProperty, SerializedProperty keys, SerializedProperty values)
        {
            var state = DictionaryViewStateStore.GetState(dictProperty);
            state.Keys = keys;
            state.Values = values;

            state.Visible.Clear();
            state.Visible.AddRange(SerializedDictionaryOperations.BuildVisibleIndices(keys, values, state.Search));

            var keyTexts = new string[keys.arraySize];
            for (var i = 0; i < keyTexts.Length; i++)
                keyTexts[i] = SerializedDictionaryOperations.PropertyKeyString(keys.GetArrayElementAtIndex(i));
            state.DuplicateWinners = DictionaryKeyUtility.FindDuplicateWinners(keyTexts);

            state.List ??= CreateList(state);
            state.List.draggable = !state.HasSearch;
            return state;
        }

        private static ReorderableList CreateList(DictionaryViewState state)
        {
            var list = new ReorderableList(state.Visible, typeof(int), draggable: true, displayHeader: true,
                displayAddButton: true, displayRemoveButton: true);

            list.drawHeaderCallback = rect => DrawColumnHeader(rect, list);
            list.elementHeightCallback = visibleIndex => ElementHeight(state, visibleIndex);
            list.drawElementCallback = (rect, visibleIndex, _, _) => DrawElement(rect, state, visibleIndex);
            list.drawNoneElementCallback = rect =>
                EditorGUI.LabelField(rect, state.HasSearch ? $"No entries match \"{state.Search}\"" : "Dictionary is empty");

            list.onReorderCallbackWithDetails = (_, oldIndex, newIndex) =>
            {
                // Dragging is disabled while searching, so visible index == real index here.
                SerializedDictionaryOperations.MoveRow(oldIndex, newIndex, state.Keys, state.Values);
            };

            list.onAddCallback = l =>
            {
                if (state.HasSearch)
                {
                    state.Search = string.Empty; // otherwise the new row could be hidden by the filter
                    DictionaryViewStateStore.SaveState(state);
                }

                SerializedDictionaryOperations.AddNewRow(state.Keys, state.Values,
                    uniqueString: SerializedDictionaryOperations.IsStringKey(state.Keys));
                l.index = state.Keys.arraySize - 1;
            };

            list.onCanRemoveCallback = l => l.index >= 0 && l.index < state.Visible.Count;
            list.onRemoveCallback = l =>
            {
                SerializedDictionaryOperations.DeleteRowAt(state.Keys, state.Values, state.Visible[l.index]);
                l.index = Mathf.Min(l.index, state.Keys.arraySize - 1);
            };

            return list;
        }

        #endregion

        #region Drawing

        private static void DrawHeader(Rect rect, SerializedProperty dictProperty, string title, DictionaryViewState state)
        {
            var count = state.Keys.arraySize;

            var menuRect = new Rect(rect.xMax - MenuButtonWidth, rect.y, MenuButtonWidth, rect.height);
            var descRect = new Rect(menuRect.x - 2f - SortButtonWidth, rect.y, SortButtonWidth, rect.height);
            var ascRect = new Rect(descRect.x - SortButtonWidth, rect.y, SortButtonWidth, rect.height);
            var searchWidth = Mathf.Min(SearchWidth, Mathf.Max(60f, rect.width * 0.35f));
            var searchRect = new Rect(ascRect.x - 4f - searchWidth, rect.y + 1f, searchWidth, rect.height - 2f);
            var foldRect = new Rect(rect.x, rect.y, Mathf.Max(0f, searchRect.x - rect.x - 4f), rect.height);

            dictProperty.isExpanded = EditorGUI.Foldout(foldRect, dictProperty.isExpanded,
                new GUIContent($"{title} ({count})"), toggleOnLabelClick: true);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            try
            {
                EditorGUI.BeginChangeCheck();
                var search = EditorGUI.TextField(searchRect, state.Search ?? string.Empty, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                {
                    state.Search = search;
                    DictionaryViewStateStore.SaveState(state);
                    if (state.HasSearch) dictProperty.isExpanded = true;
                }

                using (new EditorGUI.DisabledScope(count < 2))
                {
                    if (GUI.Button(ascRect, new GUIContent("A→Z", "Sort by key ascending"), EditorStyles.miniButtonLeft))
                        SerializedDictionaryOperations.SortByKey(state.Keys, state.Values, descending: false);
                    if (GUI.Button(descRect, new GUIContent("Z→A", "Sort by key descending"), EditorStyles.miniButtonRight))
                        SerializedDictionaryOperations.SortByKey(state.Keys, state.Values, descending: true);
                }

                if (GUI.Button(menuRect, new GUIContent("⋮", "Options"), EditorStyles.miniButton))
                    ShowOptionsMenu(dictProperty, state);
            }
            finally
            {
                EditorGUI.indentLevel = indent;
            }
        }

        private static void DrawColumnHeader(Rect rect, ReorderableList list)
        {
            var x = rect.x + (list.draggable ? DragHandleWidth : 0f);
            var width = rect.xMax - x;
            var keyWidth = width * KeyColumnRatio;
            EditorGUI.LabelField(new Rect(x, rect.y, keyWidth, rect.height), "Key", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(new Rect(x + keyWidth + ColumnGap, rect.y, width - keyWidth - ColumnGap, rect.height),
                "Value", EditorStyles.miniBoldLabel);
        }

        private static float ElementHeight(DictionaryViewState state, int visibleIndex)
        {
            if (state.Keys == null || visibleIndex < 0 || visibleIndex >= state.Visible.Count) return Line;
            var real = state.Visible[visibleIndex];
            if (real >= state.Keys.arraySize) return Line;

            var keyHeight = EditorGUI.GetPropertyHeight(state.Keys.GetArrayElementAtIndex(real), GUIContent.none, true);
            var valueHeight = EditorGUI.GetPropertyHeight(state.Values.GetArrayElementAtIndex(real), GUIContent.none, true);
            return Mathf.Max(keyHeight, valueHeight) + RowPadding * 2f;
        }

        private static void DrawElement(Rect rect, DictionaryViewState state, int visibleIndex)
        {
            if (state.Keys == null || visibleIndex < 0 || visibleIndex >= state.Visible.Count) return;
            var real = state.Visible[visibleIndex];
            if (real >= state.Keys.arraySize) return;

            var keyProp = state.Keys.GetArrayElementAtIndex(real);
            var valueProp = state.Values.GetArrayElementAtIndex(real);

            rect.y += RowPadding;
            var keyWidth = rect.width * KeyColumnRatio;
            var keyRect = new Rect(rect.x, rect.y, keyWidth, EditorGUI.GetPropertyHeight(keyProp, GUIContent.none, true));
            var valueRect = new Rect(rect.x + keyWidth + ColumnGap, rect.y, rect.width - keyWidth - ColumnGap,
                EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true));

            var winner = real < state.DuplicateWinners.Length ? state.DuplicateWinners[real] : -1;
            if (winner >= 0)
            {
                var iconRect = new Rect(keyRect.xMax - IconWidth, rect.y, IconWidth, Line);
                keyRect.width -= IconWidth;
                var tooltip = winner == real
                    ? "Duplicate key — this entry is the one kept at runtime (last occurrence wins)."
                    : $"Duplicate key — ignored at runtime; overridden by entry #{winner}.";
                GUI.Label(iconRect, new GUIContent(WarnIcon.image, tooltip));
            }

            var labelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = Mathf.Max(60f, keyRect.width * 0.4f);
                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);
                EditorGUIUtility.labelWidth = Mathf.Max(60f, valueRect.width * 0.4f);
                EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, true);
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }

            var e = Event.current;
            if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
            {
                ShowRowMenu(state, real);
                e.Use();
            }
        }

        #endregion

        #region Menus

        private static void ShowOptionsMenu(SerializedProperty dictProperty, DictionaryViewState state)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Duplicate Keys/Warn Only"), !state.PreventDuplicates,
                () => SetDedup(state, prevent: false, state.DedupMode));
            menu.AddItem(new GUIContent("Duplicate Keys/Auto Make Unique"),
                state.PreventDuplicates && state.DedupMode == DictionaryDedupMode.AutoUnique,
                () => SetDedup(state, prevent: true, DictionaryDedupMode.AutoUnique));
            menu.AddItem(new GUIContent("Duplicate Keys/Remove Duplicates (Keep First)"),
                state.PreventDuplicates && state.DedupMode == DictionaryDedupMode.Merge,
                () => SetDedup(state, prevent: true, DictionaryDedupMode.Merge));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Export JSON…"), false,
                Deferred(dictProperty, DictionaryJsonCodec.ExportJson, apply: false));
            menu.AddItem(new GUIContent("Import JSON…"), false,
                Deferred(dictProperty, DictionaryJsonCodec.ImportJson, apply: true));

            menu.AddSeparator(string.Empty);
            if (state.Keys.arraySize > 0)
                menu.AddItem(new GUIContent("Clear All"), false, Deferred(dictProperty, (keys, values) =>
                {
                    keys.arraySize = 0;
                    values.arraySize = 0;
                }, apply: true));
            else
                menu.AddDisabledItem(new GUIContent("Clear All"));

            menu.ShowAsContext();
        }

        private static void ShowRowMenu(DictionaryViewState state, int real)
        {
            var dictProperty = state.Keys.serializedObject.FindProperty(ParentPath(state.Keys.propertyPath));
            var count = state.Keys.arraySize;
            var canMove = !state.HasSearch;
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Duplicate"), false,
                Deferred(dictProperty, (k, v) => SerializedDictionaryOperations.DuplicateRow(real, k, v), apply: true));

            if (canMove && real > 0)
                menu.AddItem(new GUIContent("Move Up"), false,
                    Deferred(dictProperty, (k, v) => SerializedDictionaryOperations.MoveRow(real, real - 1, k, v), apply: true));
            else
                menu.AddDisabledItem(new GUIContent("Move Up"));

            if (canMove && real < count - 1)
                menu.AddItem(new GUIContent("Move Down"), false,
                    Deferred(dictProperty, (k, v) => SerializedDictionaryOperations.MoveRow(real, real + 1, k, v), apply: true));
            else
                menu.AddDisabledItem(new GUIContent("Move Down"));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false,
                Deferred(dictProperty, (k, v) => SerializedDictionaryOperations.DeleteRowAt(k, v, real), apply: true));

            menu.ShowAsContext();
        }

        private static void SetDedup(DictionaryViewState state, bool prevent, DictionaryDedupMode mode)
        {
            state.PreventDuplicates = prevent;
            state.DedupMode = mode;
            DictionaryViewStateStore.SaveState(state);
        }

        /// <summary>
        /// GenericMenu callbacks run after the GUI pass that built the menu, when that pass's SerializedProperty
        /// objects may be stale and the inspector will not apply changes. Re-open the targets in a fresh
        /// SerializedObject, run <paramref name="op"/>, and apply it (with Undo) ourselves.
        /// </summary>
        private static GenericMenu.MenuFunction Deferred(SerializedProperty dictProperty,
            Action<SerializedProperty, SerializedProperty> op, bool apply)
        {
            var targets = dictProperty.serializedObject.targetObjects;
            var path = dictProperty.propertyPath;

            return () =>
            {
                if (targets == null || Array.Exists(targets, t => t == null)) return;

                using var serializedObject = new SerializedObject(targets);
                var property = serializedObject.FindProperty(path);
                if (!TryResolve(property, out var keys, out var values)) return;

                op(keys, values);
                if (apply) serializedObject.ApplyModifiedProperties();
                InternalEditorUtility.RepaintAllViews();
            };
        }

        private static string ParentPath(string keysPath)
        {
            var dot = keysPath.LastIndexOf('.');
            return dot < 0 ? keysPath : keysPath.Substring(0, dot);
        }

        #endregion
    }

    /// <summary>
    /// PropertyDrawer for any field of type SerializableDictionary&lt;TKey, TValue&gt;.
    /// </summary>
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    public class SerializableDictionaryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => DictionaryField.GetHeight(property, label);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            DictionaryField.Draw(position, property, label);
            EditorGUI.EndProperty();
        }
    }
}
