using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Duplicate-key resolution policy used by <see cref="DictionaryField"/> when "Prevent duplicates" is on.
    /// Member names are shown in the toolbar popup and their int values are persisted in EditorPrefs.
    /// </summary>
    internal enum DictionaryDedupMode
    {
        AutoUnique,
        Merge
    }

    /// <summary>
    /// Per-property UI state of a <see cref="DictionaryField"/> (search, duplicate policy, selection and drag state).
    /// </summary>
    internal sealed class DictionaryViewState
    {
        public string Search;
        public bool PreventDuplicates;
        public DictionaryDedupMode DedupMode = DictionaryDedupMode.AutoUnique;
        public bool Loaded; // flag to avoid reloading
        public HashSet<int> Selected = new();
        public int? DraggingIndex; // currently dragged row original index
        public int? DragInsertIndex; // live target insert index visualization (absolute array index)
        public List<Rect> CurrentRowRects = new(); // rects for visible rows (ordered)
        public List<int> CurrentVisibleIndices = new(); // mapping visible order -> real array index
    }

    /// <summary>
    /// Caches <see cref="DictionaryViewState"/> per (target object, property path) for the editor session and
    /// persists the user-facing options (search, prevent duplicates, dedup mode) in EditorPrefs.
    /// </summary>
    internal static class DictionaryViewStateStore
    {
        private static readonly Dictionary<string, DictionaryViewState> SStates = new();

        // Persistence helpers
        private static string PrefKeyBase(SerializedProperty p) => "VuTH_DictField_" + p.serializedObject.targetObject.GetType().FullName + "_" + p.propertyPath;

        /// <summary>Returns the cached state for <paramref name="p"/>, creating it on first access.</summary>
        public static DictionaryViewState GetState(SerializedProperty p)
        {
            if (p == null) return new DictionaryViewState();
            var key = p.serializedObject.targetObject.GetEntityId() + ":" + p.propertyPath;
            if (!SStates.TryGetValue(key, out var st))
            {
                st = new DictionaryViewState();
                SStates[key] = st;
            }

            return st;
        }

        /// <summary>Loads persisted options into <paramref name="st"/> once.</summary>
        public static void LoadState(SerializedProperty p, DictionaryViewState st)
        {
            if (st.Loaded) return;
            string baseKey = PrefKeyBase(p);
            st.Search = EditorPrefs.GetString(baseKey + "_search", st.Search);
            st.PreventDuplicates = EditorPrefs.GetBool(baseKey + "_prevent", st.PreventDuplicates);
            st.DedupMode = (DictionaryDedupMode)EditorPrefs.GetInt(baseKey + "_dedup", (int)st.DedupMode);
            st.Loaded = true;
        }

        /// <summary>Persists the options of <paramref name="st"/> to EditorPrefs.</summary>
        public static void SaveState(SerializedProperty p, DictionaryViewState st)
        {
            string baseKey = PrefKeyBase(p);
            EditorPrefs.SetString(baseKey + "_search", st.Search ?? string.Empty);
            EditorPrefs.SetBool(baseKey + "_prevent", st.PreventDuplicates);
            EditorPrefs.SetInt(baseKey + "_dedup", (int)st.DedupMode);
        }
    }
}
