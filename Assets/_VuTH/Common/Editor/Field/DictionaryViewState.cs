using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Duplicate-key resolution policy used by <see cref="DictionaryField"/> when "Prevent duplicates" is on.
    /// Int values are persisted in EditorPrefs — do not reorder.
    /// </summary>
    internal enum DictionaryDedupMode
    {
        /// <summary>Rename later duplicates (string keys get a numeric suffix).</summary>
        AutoUnique,

        /// <summary>Delete later duplicates, keeping the first entry.</summary>
        Merge
    }

    /// <summary>
    /// Per-property UI state of a <see cref="DictionaryField"/>: persisted options plus the cached
    /// <see cref="ReorderableList"/> and the rows it currently shows.
    /// </summary>
    internal sealed class DictionaryViewState
    {
        public string Search = string.Empty;
        public bool PreventDuplicates;
        public DictionaryDedupMode DedupMode = DictionaryDedupMode.AutoUnique;
        public bool Loaded;
        public string PrefKey;

        /// <summary>Real array indices of the rows shown by <see cref="List"/> (search-filtered).</summary>
        public readonly List<int> Visible = new();

        /// <summary>For each real row: index of the row whose value wins at runtime, or -1 if the key is unique.</summary>
        public int[] DuplicateWinners = System.Array.Empty<int>();

        public ReorderableList List;

        // Valid only while the list is being drawn / measured in the current GUI pass.
        public SerializedProperty Keys;
        public SerializedProperty Values;

        public bool HasSearch => !string.IsNullOrEmpty(Search);
    }

    /// <summary>
    /// Caches <see cref="DictionaryViewState"/> per (target object, property path) for the editor session and
    /// persists the user-facing options (search, prevent duplicates, dedup mode) in EditorPrefs.
    /// </summary>
    internal static class DictionaryViewStateStore
    {
        private static readonly Dictionary<string, DictionaryViewState> SStates = new();

        private static string PrefKeyBase(SerializedProperty p) =>
            "VuTH_DictField_" + p.serializedObject.targetObject.GetType().FullName + "_" + p.propertyPath;

        /// <summary>Returns the cached state for <paramref name="p"/>, creating and loading it on first access.</summary>
        public static DictionaryViewState GetState(SerializedProperty p)
        {
            var key = p.serializedObject.targetObject.GetEntityId() + ":" + p.propertyPath;
            if (!SStates.TryGetValue(key, out var st))
            {
                st = new DictionaryViewState();
                SStates[key] = st;
            }

            if (!st.Loaded)
            {
                st.PrefKey = PrefKeyBase(p);
                LoadState(st);
            }
            return st;
        }

        private static void LoadState(DictionaryViewState st)
        {
            var baseKey = st.PrefKey;
            st.Search = EditorPrefs.GetString(baseKey + "_search", st.Search);
            st.PreventDuplicates = EditorPrefs.GetBool(baseKey + "_prevent", st.PreventDuplicates);
            st.DedupMode = (DictionaryDedupMode)EditorPrefs.GetInt(baseKey + "_dedup", (int)st.DedupMode);
            st.Loaded = true;
        }

        /// <summary>Persists the options of <paramref name="st"/> to EditorPrefs.</summary>
        public static void SaveState(DictionaryViewState st)
        {
            var baseKey = st.PrefKey;
            EditorPrefs.SetString(baseKey + "_search", st.Search ?? string.Empty);
            EditorPrefs.SetBool(baseKey + "_prevent", st.PreventDuplicates);
            EditorPrefs.SetInt(baseKey + "_dedup", (int)st.DedupMode);
        }
    }
}
