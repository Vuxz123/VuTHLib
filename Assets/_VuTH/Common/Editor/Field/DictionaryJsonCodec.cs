using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Minimal JSON import/export for the key/value backing arrays drawn by <see cref="DictionaryField"/>.
    /// The format is an array of <c>{"key":...,"value":...}</c> objects holding primitive values only.
    /// String-level encoding/parsing is pure and unit-testable; the SerializedProperty members are editor-only.
    /// </summary>
    public static class DictionaryJsonCodec
    {
        /// <summary>Escapes backslashes and double quotes. Returns an empty string for null/empty input.</summary>
        public static string EscapeString(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("\\", @"\\").Replace("\"", "\\\"");
        }

        /// <summary>Builds the JSON array text from already-encoded key/value JSON fragments.</summary>
        public static string FormatArray(IReadOnlyList<(string keyJson, string valueJson)> entries)
        {
            var parts = new List<string>();
            foreach (var (keyJson, valJson) in entries)
                parts.Add($"{{\"key\":{keyJson},\"value\":{valJson}}}");
            return "[" + string.Join(",", parts) + "]";
        }

        /// <summary>
        /// Parses a simple JSON array of <c>{"key":...,"value":...}</c> objects into raw (unquoted/unescaped) strings.
        /// Non-string values are returned trimmed as-is (e.g. <c>42</c>, <c>true</c>, <c>null</c>). Unknown properties are ignored.
        /// Returns an empty list for null/whitespace input; throws <see cref="Exception"/> on malformed input.
        /// </summary>
        public static List<(string key, string val)> ParseSimpleArray(string json)
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

        /// <summary>Shows a save dialog and writes the key/value arrays as JSON.</summary>
        internal static void ExportJson(SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            string path = EditorUtility.SaveFilePanel("Export Dictionary JSON", Application.dataPath, "dictionary_export", "json");
            if (string.IsNullOrEmpty(path)) return;
            var entries = new List<(string keyJson, string valueJson)>();
            int count = Mathf.Min(keysProp.arraySize, valuesProp.arraySize);
            for (int i = 0; i < count; i++)
            {
                var k = keysProp.GetArrayElementAtIndex(i);
                var v = valuesProp.GetArrayElementAtIndex(i);
                string keyJson = SerializePrimitive(k);
                string valJson = SerializePrimitive(v);
                entries.Add((keyJson, valJson));
            }
            string json = FormatArray(entries);
            System.IO.File.WriteAllText(path, json);
            AssetDatabase.Refresh();
        }

        /// <summary>Shows an open dialog and replaces the key/value arrays with the parsed JSON entries.</summary>
        internal static void ImportJson(SerializedProperty keysProp, SerializedProperty valuesProp)
        {
            string path = EditorUtility.OpenFilePanel("Import Dictionary JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
            string json = System.IO.File.ReadAllText(path);
            try
            {
                var list = ParseSimpleArray(json);
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
                    return "\"" + EscapeString(p.stringValue ?? string.Empty) + "\"";
                case SerializedPropertyType.Enum:
                    return "\"" + EscapeString(p.enumDisplayNames != null && p.enumDisplayNames.Length > p.enumValueIndex ? p.enumDisplayNames[p.enumValueIndex] : p.enumValueIndex.ToString()) + "\"";
                case SerializedPropertyType.ObjectReference:
                    {
                        var obj = p.objectReferenceValue;
                        if (!obj) return "null";
                        string assetPath = AssetDatabase.GetAssetPath(obj);
                        string name = obj.name;
                        return "{\"name\":\"" + EscapeString(name) + "\",\"path\":\"" + EscapeString(assetPath) + "\"}";
                    }
                default:
                    return "null"; // unsupported complex types
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
    }
}
