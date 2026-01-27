using System;
using System.Collections.Generic;
using System.Globalization;
using _VuTH.Common.Editor.Helpers;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Helper API to draw a SerializableDictionary property from custom inspectors.
    /// Usage: DictionaryField.Draw(property, label);
    /// </summary>
    public static class DictionaryField
    {
        private enum DedupMode
        {
            AutoUnique,
            Merge
        }

        private class ViewState
        {
            public string Search;
            public bool PreventDuplicates;
            public DedupMode DedupMode = DedupMode.AutoUnique;
            public bool Loaded; // flag to avoid reloading
            public HashSet<int> Selected = new();
            public int? DraggingIndex; // currently dragged row original index
            public int? DragInsertIndex; // live target insert index visualization (absolute array index)
            public List<Rect> CurrentRowRects = new(); // rects for visible rows (ordered)
            public List<int> CurrentVisibleIndices = new(); // mapping visible order -> real array index
        }

        // Persistence helpers
        private static string PrefKeyBase(SerializedProperty p) => "VuTH_DictField_" + p.serializedObject.targetObject.GetType().FullName + "_" + p.propertyPath;
        private static void LoadState(SerializedProperty p, ViewState st)
        {
            if (st.Loaded) return;
            string baseKey = PrefKeyBase(p);
            st.Search = EditorPrefs.GetString(baseKey + "_search", st.Search);
            st.PreventDuplicates = EditorPrefs.GetBool(baseKey + "_prevent", st.PreventDuplicates);
            st.DedupMode = (DedupMode)EditorPrefs.GetInt(baseKey + "_dedup", (int)st.DedupMode);
            st.Loaded = true;
        }
        private static void SaveState(SerializedProperty p, ViewState st)
        {
            string baseKey = PrefKeyBase(p);
            EditorPrefs.SetString(baseKey + "_search", st.Search ?? string.Empty);
            EditorPrefs.SetBool(baseKey + "_prevent", st.PreventDuplicates);
            EditorPrefs.SetInt(baseKey + "_dedup", (int)st.DedupMode);
        }

        private static readonly Dictionary<string, ViewState> SStates = new();

        private static ViewState GetState(SerializedProperty p)
        {
            if (p == null) return new ViewState();
            var key = p.serializedObject.targetObject.GetEntityId() + ":" + p.propertyPath;
            if (!SStates.TryGetValue(key, out var st))
            {
                st = new ViewState();
                SStates[key] = st;
            }

            return st;
        }

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

            var state = GetState(dictProperty);
            LoadState(dictProperty, state);

            // Header context menu (JSON export/import)
            Rect headerRect = GUILayoutUtility.GetLastRect();
            HandleHeaderContextMenu(headerRect, keysProp, valuesProp);

            // Toolbar: Search, Sort, Duplicate policy + selection actions
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Search:", GUILayout.Width(50));
            var prevSearch = state.Search;
            state.Search = GUILayout.TextField(state.Search ?? string.Empty, GUILayout.MinWidth(120));
            if (state.Search != prevSearch)
            {
                SaveState(dictProperty, state);
            }
            if (!string.IsNullOrEmpty(state.Search))
            {
                if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    state.Search = string.Empty;
                    SaveState(dictProperty, state);
                    GUI.FocusControl(null); // clear focus
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("A→Z", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                SortByKey(keysProp, valuesProp, false);
            }

            if (GUILayout.Button("Z→A", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                SortByKey(keysProp, valuesProp, true);
            }

            GUILayout.Space(8);
            bool prevPrevent = state.PreventDuplicates;
            var newPrevent = GUILayout.Toggle(state.PreventDuplicates, "Prevent duplicates", EditorStyles.toolbarButton);
            if (newPrevent != prevPrevent)
            {
                state.PreventDuplicates = newPrevent;
                SaveState(dictProperty, state);
            }
            if (state.PreventDuplicates)
            {
                var prevMode = state.DedupMode;
                state.DedupMode = (DedupMode)EditorGUILayout.EnumPopup(state.DedupMode, GUILayout.Width(110));
                if (state.DedupMode != prevMode)
                {
                    SaveState(dictProperty, state);
                }
                ApplyDuplicatePolicy(keysProp, valuesProp, state.DedupMode);
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
                DeleteSelected(keysProp, valuesProp, state.Selected);
                state.Selected.Clear();
            }

            EditorGUILayout.EndHorizontal();

            // Build visible indices using search filter
            var visible = new List<int>(keysProp.arraySize);
            string search = state.Search ?? string.Empty;
            bool hasSearch = !string.IsNullOrEmpty(search);
            string searchLower = hasSearch ? search.ToLowerInvariant() : string.Empty;

            for (int i = 0; i < keysProp.arraySize; i++)
            {
                if (!hasSearch)
                {
                    visible.Add(i);
                    continue;
                }

                var keyStr = (PropertyKeyString(keysProp.GetArrayElementAtIndex(i)) ?? string.Empty).ToLowerInvariant();
                var valStr = (PropertyToString(valuesProp.GetArrayElementAtIndex(i)) ?? string.Empty)
                    .ToLowerInvariant();
                if (keyStr.Contains(searchLower) || valStr.Contains(searchLower))
                    visible.Add(i);
            }

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

                HandleRowSelection(indexLabelRect, i, state);
                HandleRowDrag(rowStart, i, state, keysProp, valuesProp);

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

                var keyId = PropertyKeyString(keyProp);
                if (!string.IsNullOrEmpty(keyId))
                {
                    if (!seen.Add(keyId))
                        EditorGUILayout.HelpBox("Duplicate key detected; only the last one will be used at runtime.",
                            MessageType.Warning);
                }
            }

            // Precise drag insertion index update (only when no search filter to avoid ambiguous reordering of filtered subset)
            if (state.DraggingIndex.HasValue && string.IsNullOrEmpty(state.Search))
            {
                var eDrag = Event.current;
                if (eDrag.type == EventType.MouseDrag || eDrag.type == EventType.MouseMove)
                {
                    state.DragInsertIndex = ComputePreciseInsertIndex(state, keysProp.arraySize, eDrag.mousePosition.y);
                    // Request repaint for visual feedback
                    if (state.DragInsertIndex.HasValue) EditorWindow.focusedWindow?.Repaint();
                }
            }

            // Draw drag insertion line feedback
            if (state.DragInsertIndex.HasValue && state.DraggingIndex.HasValue && Event.current.type == EventType.Repaint)
            {
                int insertAbs = state.DragInsertIndex.Value;
                // Determine y position relative to stored rects
                float y;
                var rects = state.CurrentRowRects;
                if (rects.Count == 0)
                    y = headerRect.yMax + 4f;
                else if (insertAbs <= state.CurrentVisibleIndices[0])
                    y = rects[0].yMin - 2f;
                else if (insertAbs > state.CurrentVisibleIndices[rects.Count - 1])
                    y = rects[^1].yMax + 2f;
                else
                {
                    // Find indices bounding insertAbs
                    int lowerIdx = -1;
                    int upperIdx = -1;
                    for (int r = 0; r < state.CurrentVisibleIndices.Count; r++)
                    {
                        int real = state.CurrentVisibleIndices[r];
                        if (real < insertAbs) lowerIdx = r;
                        if (real >= insertAbs) { upperIdx = r; break; }
                    }
                    if (upperIdx >= 0 && lowerIdx >= 0)
                    {
                        y = (rects[lowerIdx].yMax + rects[upperIdx].yMin) * 0.5f;
                    }
                    else if (upperIdx >= 0)
                        y = rects[upperIdx].yMin - 2f;
                    else // fallback
                        y = rects[^1].yMax + 2f;
                }
                var lineRect = new Rect(0, y, EditorGUIUtility.currentViewWidth, 2f);
                EditorGUI.DrawRect(lineRect, new Color(0.2f, 0.8f, 1f, 0.9f));
            }

            // Add row buttons
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Empty", GUILayout.Width(90)))
            {
                AddNewRow(keysProp, valuesProp, uniqueString: false);
            }
            if (IsStringKey(keysProp) && GUILayout.Button("Add Template", GUILayout.Width(100)))
            {
                AddNewRow(keysProp, valuesProp, uniqueString: true);
            }
            EditorGUILayout.EndHorizontal();

            // Apply single-row removal if requested
            if (removeIndex >= 0 && removeIndex < keysProp.arraySize)
            {
                int before = keysProp.arraySize;
                keysProp.DeleteArrayElementAtIndex(removeIndex);
                if (keysProp.arraySize == before) keysProp.DeleteArrayElementAtIndex(removeIndex);
                before = valuesProp.arraySize;
                valuesProp.DeleteArrayElementAtIndex(removeIndex);
                if (valuesProp.arraySize == before) valuesProp.DeleteArrayElementAtIndex(removeIndex);
            }

            EditorGUI.indentLevel--;

            // end outer box
            EditorGUILayout.EndVertical();
        }

        private static void SortByKey(SerializedProperty keysProp, SerializedProperty valuesProp, bool descending)
        {
            int n = keysProp.arraySize;
            if (n <= 1) return;

            int Compare(int a, int b)
            {
                var sa = PropertyKeyString(keysProp.GetArrayElementAtIndex(a)) ?? string.Empty;
                var sb = PropertyKeyString(keysProp.GetArrayElementAtIndex(b)) ?? string.Empty;
                int cmp = string.Compare(sa, sb, StringComparison.Ordinal);
                return descending ? -cmp : cmp;
            }

            // Stable insertion sort via MoveArrayElement
            for (int i = 1; i < n; i++)
            {
                int j = i;
                while (j > 0 && Compare(j - 1, j) > 0)
                {
                    keysProp.MoveArrayElement(j, j - 1);
                    valuesProp.MoveArrayElement(j, j - 1);
                    j--;
                }
            }
        }

        private static void ApplyDuplicatePolicy(SerializedProperty keysProp, SerializedProperty valuesProp,
            DedupMode mode)
        {
            var seen = new HashSet<string>();
            int i = 0;
            while (i < keysProp.arraySize)
            {
                var keyP = keysProp.GetArrayElementAtIndex(i);
                string id = PropertyKeyString(keyP) ?? string.Empty;
                if (id.Length == 0)
                {
                    // Allow empty key; treat as its own ID to avoid merging all empties
                    id = "<empty>" + i.ToString();
                }

                if (seen.Add(id))
                {
                    i++;
                    continue;
                }

                if (mode == DedupMode.Merge)
                {
                    // Remove this duplicate entry (keep the first)
                    int before = keysProp.arraySize;
                    keysProp.DeleteArrayElementAtIndex(i);
                    if (keysProp.arraySize == before)
                        keysProp.DeleteArrayElementAtIndex(i);

                    before = valuesProp.arraySize;
                    valuesProp.DeleteArrayElementAtIndex(i);
                    if (valuesProp.arraySize == before)
                        valuesProp.DeleteArrayElementAtIndex(i);
                    // Do not increment i; next element shifted into i
                }
                else
                {
                    // Auto-unique: try to modify key to a unique value
                    AutoUniqueKey(keyP, seen);
                    // Recompute ID after change and add to seen
                    string newId = PropertyKeyString(keyP) ?? string.Empty;
                    if (newId.Length == 0)
                        newId = "<auto>" + i.ToString();
                    seen.Add(newId);
                    i++;
                }
            }
        }

        private static void AutoUniqueKey(SerializedProperty keyProp, HashSet<string> used)
        {
            switch (keyProp.propertyType)
            {
                case SerializedPropertyType.String:
                {
                    string baseStr = keyProp.stringValue ?? string.Empty;
                    string attempt = baseStr;
                    int n = 1;
                    while (used.Contains(attempt))
                    {
                        attempt = string.IsNullOrEmpty(baseStr) ? n.ToString() : ($"{baseStr} ({n})");
                        n++;
                    }

                    keyProp.stringValue = attempt;
                    break;
                }
                case SerializedPropertyType.Integer:
                {
                    long v = keyProp.longValue; // covers int/long
                    while (used.Contains(v.ToString())) v++;
                    keyProp.longValue = v;
                    break;
                }
                case SerializedPropertyType.Enum:
                {
                    int v = keyProp.enumValueIndex;
                    int guard = 0;
                    while (used.Contains(v.ToString()) && guard < 1024)
                    {
                        v = (v + 1) % keyProp.enumNames.Length;
                        guard++;
                    }

                    keyProp.enumValueIndex = v;
                    break;
                }
                case SerializedPropertyType.Float:
                {
                    double v = keyProp.doubleValue;
                    int guard = 0;
                    while (used.Contains(v.ToString(CultureInfo.InvariantCulture)) && guard < 1024)
                    {
                        v += 1.0;
                        guard++;
                    }

                    keyProp.doubleValue = v;
                    break;
                }
            }
        }

        private static void ResetProperty(SerializedProperty prop)
        {
            if (prop == null) return;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.longValue = 0; break;
                case SerializedPropertyType.Float:
                    prop.doubleValue = 0d; break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = false; break;
                case SerializedPropertyType.String:
                    prop.stringValue = string.Empty; break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = null; break;
                case SerializedPropertyType.Color:
                    prop.colorValue = Color.white; break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = Vector2.zero; break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = Vector3.zero; break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = Vector4.zero; break;
                case SerializedPropertyType.Rect:
                    prop.rectValue = new Rect(); break;
                case SerializedPropertyType.Bounds:
                    prop.boundsValue = new Bounds(); break;
                case SerializedPropertyType.Quaternion:
                    prop.quaternionValue = Quaternion.identity; break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = 0; break;
                default:
                    if (prop.isArray)
                    {
                        prop.ClearArray();
                    }
                    else if (prop.propertyType == SerializedPropertyType.Generic)
                    {
                        var copy = prop.Copy();
                        var end = prop.GetEndProperty();
                        bool enterChildren = true;
                        while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
                        {
                            enterChildren = false;
                            ResetProperty(copy);
                        }
                    }

                    break;
            }
        }

        private static string PropertyKeyString(SerializedProperty p)
        {
            if (p == null) return null;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                {
                    // Distinguish between int and long via p.type
                    if (p.type is nameof(Int64) or "long")
                        return p.longValue.ToString();
                    return p.intValue.ToString();
                }
                case SerializedPropertyType.Float:
                {
                    // Distinguish between float and double via p.type
                    if (p.type is nameof(Double) or "double")
                        return p.doubleValue.ToString(CultureInfo.InvariantCulture);
                    return p.floatValue.ToString(CultureInfo.InvariantCulture);
                }
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.String: return p.stringValue ?? string.Empty;
                case SerializedPropertyType.Enum:
                    return p.enumDisplayNames != null && p.enumDisplayNames.Length > p.enumValueIndex
                        ? p.enumDisplayNames[p.enumValueIndex]
                        : p.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return p.objectReferenceValue ? p.objectReferenceValue.name : "null";
                default:
                    return FlattenGenericToString(p);
            }
        }

        private static string PropertyToString(SerializedProperty p)
        {
            if (p == null) return string.Empty;
            return p.propertyType switch
            {
                SerializedPropertyType.Integer => p.type is nameof(Int64) or "long"
                    ? p.longValue.ToString()
                    : p.intValue.ToString(),
                SerializedPropertyType.Float => p.type is nameof(Double) or "double"
                    ? p.doubleValue.ToString(CultureInfo.InvariantCulture)
                    : p.floatValue.ToString(CultureInfo.InvariantCulture),
                SerializedPropertyType.Boolean => p.boolValue.ToString(),
                SerializedPropertyType.String => p.stringValue ?? string.Empty,
                SerializedPropertyType.Enum => p.enumDisplayNames != null &&
                                               p.enumDisplayNames.Length > p.enumValueIndex
                    ? p.enumDisplayNames[p.enumValueIndex]
                    : p.enumValueIndex.ToString(),
                SerializedPropertyType.ObjectReference => p.objectReferenceValue
                    ? p.objectReferenceValue.name
                    : "null",
                _ => FlattenGenericToString(p)
            };
        }

        private static string FlattenGenericToString(SerializedProperty p)
        {
            // Attempt to build a simple string by concatenating first-level child values
            if (p == null) return string.Empty;
            if (p.propertyType != SerializedPropertyType.Generic) return p.displayName ?? p.propertyPath;
            var copy = p.Copy();
            var end = p.GetEndProperty();
            var enterChildren = true;
            System.Text.StringBuilder sb = null;
            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;
                var part = copy.propertyType switch
                {
                    SerializedPropertyType.String => copy.stringValue,
                    SerializedPropertyType.Integer => copy.type is nameof(Int64) or "long"
                        ? copy.longValue.ToString()
                        : copy.intValue.ToString(),
                    SerializedPropertyType.Float => copy.type is nameof(Double) or "double"
                        ? copy.doubleValue.ToString(CultureInfo.InvariantCulture)
                        : copy.floatValue.ToString(CultureInfo.InvariantCulture),
                    SerializedPropertyType.Boolean => copy.boolValue.ToString(),
                    SerializedPropertyType.Enum => copy.enumDisplayNames != null &&
                                                   copy.enumDisplayNames.Length > copy.enumValueIndex
                        ? copy.enumDisplayNames[copy.enumValueIndex]
                        : copy.enumValueIndex.ToString(),
                    SerializedPropertyType.ObjectReference => copy.objectReferenceValue
                        ? copy.objectReferenceValue.name
                        : "null",
                    _ => string.Empty
                };
                if (string.IsNullOrEmpty(part)) continue;
                sb ??= new System.Text.StringBuilder();
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(part);
            }

            return sb != null ? sb.ToString() : (p.displayName ?? p.propertyPath);
        }

        private static bool IsStringKey(SerializedProperty keysProp)
        {
            if (keysProp.arraySize == 0)
            {
                // Try creating temp element to inspect (not ideal). We'll fallback to checking property type name.
                return keysProp.propertyType == SerializedPropertyType.Generic && keysProp.type.Contains("String") || keysProp.type == "string";
            }
            var first = keysProp.GetArrayElementAtIndex(0);
            return first.propertyType == SerializedPropertyType.String;
        }

        private static void AddNewRow(SerializedProperty keysProp, SerializedProperty valuesProp, bool uniqueString)
        {
            keysProp.arraySize++;
            valuesProp.arraySize = keysProp.arraySize;
            var newKey = keysProp.GetArrayElementAtIndex(keysProp.arraySize - 1);
            var newVal = valuesProp.GetArrayElementAtIndex(valuesProp.arraySize - 1);
            ResetProperty(newKey);
            ResetProperty(newVal);
            if (uniqueString && newKey.propertyType == SerializedPropertyType.String)
            {
                // Generate unique key base
                string baseStr = "NewKey";
                string attempt = baseStr;
                int n = 1;
                var existing = new HashSet<string>();
                for (int i = 0; i < keysProp.arraySize - 1; i++)
                {
                    var k = keysProp.GetArrayElementAtIndex(i);
                    if (k.propertyType == SerializedPropertyType.String)
                        existing.Add(k.stringValue);
                }
                while (existing.Contains(attempt))
                {
                    attempt = baseStr + n;
                    n++;
                }
                newKey.stringValue = attempt;
            }
        }

        private static void ShowRowContextMenu(int index, 
            SerializedProperty keysProp, 
            SerializedProperty valuesProp, 
            Action<int> onRemove)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateRow(index, keysProp, valuesProp));
            if (index > 0)
                menu.AddItem(new GUIContent("Move Up"), false, () => MoveRow(index, index - 1, keysProp, valuesProp));
            else
                menu.AddDisabledItem(new GUIContent("Move Up"));
            if (index < keysProp.arraySize - 1)
                menu.AddItem(new GUIContent("Move Down"), false, () => MoveRow(index, index + 1, keysProp, valuesProp));
            else
                menu.AddDisabledItem(new GUIContent("Move Down"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, () => onRemove?.Invoke(index));
            menu.ShowAsContext();
        }

        private static void DuplicateRow(int index, SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            // Insert duplicate for keys
            keysProp.InsertArrayElementAtIndex(index);
            valuesProp.InsertArrayElementAtIndex(index);
            // After insertion, new duplicated elements are at 'index'. Move original down one so duplicate follows original
            MoveRow(index, index + 1, keysProp, valuesProp);
        }

        private static void MoveRow(int from, int to, SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            if (from == to) return;
            keysProp.MoveArrayElement(from, to);
            valuesProp.MoveArrayElement(from, to);
        }

        private static void DeleteSelected(SerializedProperty keysProp, SerializedProperty valuesProp, HashSet<int> selected)
        {
            // Delete in descending order to preserve indices
            var list = new List<int>(selected);
            list.Sort();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                int idx = list[i];
                int before = keysProp.arraySize;
                keysProp.DeleteArrayElementAtIndex(idx);
                if (keysProp.arraySize == before) keysProp.DeleteArrayElementAtIndex(idx);
                before = valuesProp.arraySize;
                valuesProp.DeleteArrayElementAtIndex(idx);
                if (valuesProp.arraySize == before) valuesProp.DeleteArrayElementAtIndex(idx);
            }
        }

        private static void HandleHeaderContextMenu(Rect headerRect, SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            var e = Event.current;
            if (e.type == EventType.ContextClick && headerRect.Contains(e.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Export JSON"), false, () => ExportJson(keysProp, valuesProp));
                menu.AddItem(new GUIContent("Import JSON"), false, () => ImportJson(keysProp, valuesProp));
                menu.ShowAsContext();
                e.Use();
            }
        }

        private static void ExportJson(SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            string path = EditorUtility.SaveFilePanel("Export Dictionary JSON", Application.dataPath, "dictionary_export", "json");
            if (string.IsNullOrEmpty(path)) return;
            var entries = new List<string>();
            int count = Mathf.Min(keysProp.arraySize, valuesProp.arraySize);
            for (int i = 0; i < count; i++)
            {
                var k = keysProp.GetArrayElementAtIndex(i);
                var v = valuesProp.GetArrayElementAtIndex(i);
                string keyJson = SerializePrimitive(k);
                string valJson = SerializePrimitive(v);
                entries.Add($"{{\"key\":{keyJson},\"value\":{valJson}}}");
            }
            string json = "[" + string.Join(",", entries) + "]";
            System.IO.File.WriteAllText(path, json);
            AssetDatabase.Refresh();
        }

        private static void ImportJson(SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            string path = EditorUtility.OpenFilePanel("Import Dictionary JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
            string json = System.IO.File.ReadAllText(path);
            try
            {
                var list = ParseSimpleJsonArray(json);
                keysProp.arraySize = list.Count;
                valuesProp.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    var (keyStr, valStr) = list[i];
                    var k = keysProp.GetArrayElementAtIndex(i);
                    var v = valuesProp.GetArrayElementAtIndex(i);
                    AssignPrimitive(k, keyStr);
                    AssignPrimitive(v, valStr);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Import JSON failed: " + ex.Message);
            }
        }

        private static string SerializePrimitive(SerializedProperty p)
        {
            if (p == null) return "null";
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return p.longValue.ToString();
                case SerializedPropertyType.Float:
                    return p.doubleValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:
                    return p.boolValue ? "true" : "false";
                case SerializedPropertyType.String:
                    return "\"" + EscapeJsonString(p.stringValue ?? string.Empty) + "\"";
                case SerializedPropertyType.Enum:
                    return "\"" + EscapeJsonString(p.enumDisplayNames != null && p.enumDisplayNames.Length > p.enumValueIndex ? p.enumDisplayNames[p.enumValueIndex] : p.enumValueIndex.ToString()) + "\"";
                case SerializedPropertyType.ObjectReference:
                    {
                        var obj = p.objectReferenceValue;
                        if (!obj) return "null";
                        string assetPath = AssetDatabase.GetAssetPath(obj);
                        string name = obj.name;
                        return "{\"name\":\"" + EscapeJsonString(name) + "\",\"path\":\"" + EscapeJsonString(assetPath) + "\"}";
                    }
                default:
                    return "null"; // unsupported complex types
            }
        }

        private static string EscapeJsonString(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("\\", @"\\").Replace("\"", "\\\"");
        }

        private static List<(string key, string val)> ParseSimpleJsonArray(string json)
        {
            var list = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            int i = 0;

            void SkipWhitespace() { while (i < json.Length && char.IsWhiteSpace(json[i])) i++; }
            string ParseString()
            {
                if (i >= json.Length || json[i] != '"') throw new Exception("Expected string quote");
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < json.Length)
                {
                    char c = json[i++];
                    if (c == '"') break;
                    if (c == '\\' && i < json.Length)
                    {
                        var esc = json[i++];
                        var mapped = esc switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '"' => '"',
                            '\\' => '\\',
                            _ => esc
                        };
                        sb.Append(mapped);
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            SkipWhitespace();
            if (i >= json.Length || json[i] != '[') throw new Exception("JSON must start with [");
            i++; // skip [
            while (true)
            {
                SkipWhitespace();
                if (i < json.Length && json[i] == ']')
                {
                    i++; 
                    break;
                }
                if (i >= json.Length || json[i] != '{') throw new Exception("Expected object {");
                i++; // skip {
                string keyVal = null;
                string valueVal = null;
                while (true)
                {
                    SkipWhitespace();
                    if (i < json.Length && json[i] == '}') { i++; break; }
                    var propName = ParseString();
                    SkipWhitespace();
                    if (i >= json.Length || json[i] != ':') throw new Exception("Expected :");
                    i++;
                    SkipWhitespace();
                    var propVal = ParseValue();
                    switch (propName)
                    {
                        case "key":
                            keyVal = propVal;
                            break;
                        case "value":
                            valueVal = propVal;
                            break;
                    }
                    SkipWhitespace();
                    if (i < json.Length && json[i] == ',') { i++; continue; }
                    if (i < json.Length && json[i] == '}') { i++; break; }
                }
                list.Add((keyVal, valueVal));
                SkipWhitespace();
                if (i < json.Length && json[i] == ',')
                {
                    i++; 
                    continue;
                }

                if (i < json.Length && json[i] == ']')
                {
                    i++; 
                    break;
                }
            }
            return list;

            string ParseValue()
            {
                if (i >= json.Length) return null;
                if (json[i] == '"') return ParseString();
                int start = i;
                while (i < json.Length && ",}]".IndexOf(json[i]) == -1) i++;
                return json.Substring(start, i - start).Trim();
            }
        }

        private static void AssignPrimitive(SerializedProperty p, string raw)
        {
            if (p == null) return;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (long.TryParse(raw, out var li)) p.longValue = li; break;
                case SerializedPropertyType.Float:
                    if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) p.doubleValue = d; break;
                case SerializedPropertyType.Boolean:
                    if (raw == "true" || raw == "false") p.boolValue = raw == "true"; break;
                case SerializedPropertyType.String:
                    p.stringValue = raw ?? string.Empty; break;
                case SerializedPropertyType.Enum:
                    // Try match display name
                    int idx = Array.IndexOf(p.enumDisplayNames, raw);
                    if (idx >= 0) p.enumValueIndex = idx; break;
                case SerializedPropertyType.ObjectReference:
                    // Cannot reliably restore object references from JSON without GUID; skip
                    break;
            }
        }

        private static void HandleRowSelection(Rect clickRect, int index, ViewState state)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || !clickRect.Contains(e.mousePosition)) return;
            var ctrl = e.control || e.command;
            var shift = e.shift;
            if (ctrl)
            {
                if (!state.Selected.Add(index)) state.Selected.Remove(index);
            }
            else if (shift)
            {
                if (state.Selected.Count == 0)
                    state.Selected.Add(index);
                else
                {
                    int anchor = -1;
                    int minDist = int.MaxValue;
                    foreach (var sel in state.Selected)
                    {
                        int dist = Mathf.Abs(sel - index);
                        if (dist < minDist) { minDist = dist; anchor = sel; }
                    }
                    if (anchor >= 0)
                    {
                        int a = Mathf.Min(anchor, index);
                        int b = Mathf.Max(anchor, index);
                        state.Selected.Clear();
                        for (int iSel = a; iSel <= b; iSel++) state.Selected.Add(iSel);
                    }
                }
            }
            else
            {
                state.Selected.Clear();
                state.Selected.Add(index);
            }
            e.Use();
        }

        private static void HandleRowDrag(Rect rowRect, 
            int index, 
            ViewState state, 
            SerializedProperty keysProp, 
            SerializedProperty valuesProp)
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (rowRect.Contains(e.mousePosition) && e.button == 0)
                    {
                        float localX = e.mousePosition.x - rowRect.xMin;
                        if (localX <= 20f)
                        {
                            state.DraggingIndex = index;
                            state.DragInsertIndex = index; // initialize insertion at self
                            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                            e.Use();
                        }
                    }
                    break;
                case EventType.MouseDrag:
                case EventType.MouseMove:
                    if (state.DraggingIndex.HasValue && GUIUtility.hotControl != 0 && !string.IsNullOrEmpty(state.Search))
                    {
                        // Fallback heuristic only when searching (precise reorder disabled under filter)
                        float dy = e.delta.y;
                        int current = state.DraggingIndex.Value;
                        if (Mathf.Abs(dy) > 1f)
                        {
                            if (dy < 0) state.DragInsertIndex = current; // before
                            else state.DragInsertIndex = current + 1; // after
                        }
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (state.DraggingIndex.HasValue && GUIUtility.hotControl != 0)
                    {
                        if (state.DragInsertIndex.HasValue && state.DragInsertIndex.Value != state.DraggingIndex.Value)
                        {
                            int from = state.DraggingIndex.Value;
                            int to = state.DragInsertIndex.Value;
                            if (to > from) to -= 1; // adjust target after removal when moving downward
                            MoveRow(from, Mathf.Clamp(to, 0, keysProp.arraySize - 1), keysProp, valuesProp);
                        }
                        state.DraggingIndex = null;
                        state.DragInsertIndex = null;
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }

        private static int? ComputePreciseInsertIndex(ViewState state, int totalCount, float mouseY)
        {
            var rects = state.CurrentRowRects;
            var indices = state.CurrentVisibleIndices;
            if (rects.Count == 0) return 0; // empty list => insert at start
            // Build list of center Y for comparison
            for (int i = 0; i < rects.Count; i++)
            {
                var centerY = (rects[i].yMin + rects[i].yMax) * 0.5f;
                if (mouseY < centerY)
                {
                    // Insert before this visible row => absolute index = indices[i]
                    return indices[i];
                }
            }
            // After last => place after last visible index (next index)
            int last = indices[indices.Count - 1];
            return Math.Min(last + 1, totalCount); // absolute position after last
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

