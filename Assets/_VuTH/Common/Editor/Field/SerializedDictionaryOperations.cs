using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Operations on the parallel key/value <see cref="SerializedProperty"/> arrays backing a SerializableDictionary:
    /// key-to-string conversion, search filtering, sorting, duplicate policies, row add/duplicate/move/delete.
    /// Used by <see cref="DictionaryField"/>.
    /// </summary>
    internal static class SerializedDictionaryOperations
    {
        /// <summary>Returns the array indices whose key or value text matches <paramref name="search"/> (all when empty).</summary>
        public static List<int> BuildVisibleIndices(SerializedProperty keysProp, SerializedProperty valuesProp, string search)
        {
            var visible = new List<int>(keysProp.arraySize);
            search ??= string.Empty;
            bool hasSearch = !string.IsNullOrEmpty(search);
            string searchLower = hasSearch ? search.ToLowerInvariant() : string.Empty;

            for (int i = 0; i < keysProp.arraySize; i++)
            {
                if (!hasSearch)
                {
                    visible.Add(i);
                    continue;
                }

                var keyStr = PropertyKeyString(keysProp.GetArrayElementAtIndex(i));
                var valStr = PropertyToString(valuesProp.GetArrayElementAtIndex(i));
                if (DictionaryKeyUtility.MatchesSearch(keyStr, valStr, searchLower))
                    visible.Add(i);
            }

            return visible;
        }

        public static void SortByKey(SerializedProperty keysProp, SerializedProperty valuesProp, bool descending)
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

        public static void ApplyDuplicatePolicy(SerializedProperty keysProp, SerializedProperty valuesProp,
            DictionaryDedupMode mode)
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

                if (mode == DictionaryDedupMode.Merge)
                {
                    // Remove this duplicate entry (keep the first)
                    DeleteRowAt(keysProp, valuesProp, i);
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
                    keyProp.stringValue = DictionaryKeyUtility.MakeUniqueString(keyProp.stringValue ?? string.Empty, used);
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

        /// <summary>String identity of a key element (used for duplicate detection, sorting and search).</summary>
        public static string PropertyKeyString(SerializedProperty p)
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

        public static bool IsStringKey(SerializedProperty keysProp)
        {
            if (keysProp.arraySize == 0)
            {
                // Try creating temp element to inspect (not ideal). We'll fallback to checking property type name.
                return keysProp.propertyType == SerializedPropertyType.Generic && keysProp.type.Contains("String") || keysProp.type == "string";
            }
            var first = keysProp.GetArrayElementAtIndex(0);
            return first.propertyType == SerializedPropertyType.String;
        }

        public static void AddNewRow(SerializedProperty keysProp, SerializedProperty valuesProp, bool uniqueString)
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
                var existing = new HashSet<string>();
                for (int i = 0; i < keysProp.arraySize - 1; i++)
                {
                    var k = keysProp.GetArrayElementAtIndex(i);
                    if (k.propertyType == SerializedPropertyType.String)
                        existing.Add(k.stringValue);
                }
                newKey.stringValue = DictionaryKeyUtility.MakeTemplateKey(existing);
            }
        }

        public static void DuplicateRow(int index, SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            // Insert duplicate for keys
            keysProp.InsertArrayElementAtIndex(index);
            valuesProp.InsertArrayElementAtIndex(index);
            // After insertion, new duplicated elements are at 'index'. Move original down one so duplicate follows original
            MoveRow(index, index + 1, keysProp, valuesProp);
        }

        public static void MoveRow(int from, int to, SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            if (from == to) return;
            keysProp.MoveArrayElement(from, to);
            valuesProp.MoveArrayElement(from, to);
        }

        public static void DeleteSelected(SerializedProperty keysProp, SerializedProperty valuesProp, HashSet<int> selected)
        {
            // Delete in descending order to preserve indices
            var list = new List<int>(selected);
            list.Sort();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                DeleteRowAt(keysProp, valuesProp, list[i]);
            }
        }

        /// <summary>
        /// Deletes the row at <paramref name="idx"/> from both arrays. Deletes twice when the first call only
        /// nulls an element instead of removing it (legacy object-reference array behaviour).
        /// </summary>
        public static void DeleteRowAt(SerializedProperty keysProp, SerializedProperty valuesProp, int idx)
        {
            int before = keysProp.arraySize;
            keysProp.DeleteArrayElementAtIndex(idx);
            if (keysProp.arraySize == before) keysProp.DeleteArrayElementAtIndex(idx);
            before = valuesProp.arraySize;
            valuesProp.DeleteArrayElementAtIndex(idx);
            if (valuesProp.arraySize == before) valuesProp.DeleteArrayElementAtIndex(idx);
        }
    }
}
