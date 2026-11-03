using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace _VuTH.Common.Editor.ScenePostprocessor
{
    /// <summary>
    /// Runs every <see cref="ISceneImportTask"/> on a scene right BEFORE it is written to disk
    /// (<see cref="EditorSceneManager.sceneSaving"/>), so whatever the tasks change is saved in that same save.
    /// <para>
    /// It used to run in <c>OnPostprocessAllAssets</c>, i.e. after the file was already written: the changes stayed in
    /// memory until the next save and left the scene dirty after every save. Scenes that are not open in the editor are
    /// not processed (their components cannot be inspected without opening them).
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class ScenePostprocessor
    {
        // Cached once per domain: tasks are discovered through TypeCache.
        private static List<ISceneImportTask> _tasks;

        static ScenePostprocessor()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            foreach (var task in GetTasks())
            {
                task.OnSceneSaving(scene, path);
            }
        }

        /// <summary>Runs all tasks for the open scene at <paramref name="scenePath"/> (also usable from tooling).</summary>
        public static void RunTasks(string scenePath)
        {
            foreach (var task in GetTasks())
            {
                task.OnSceneImported(scenePath);
            }
        }

        private static List<ISceneImportTask> GetTasks()
        {
            if (_tasks != null) return _tasks;

            _tasks = TypeCache.GetTypesDerivedFrom<ISceneImportTask>()
                .Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (ISceneImportTask)Activator.CreateInstance(t))
                .OrderBy(t => t.Order)
                .ThenBy(t => t.GetType().FullName, StringComparer.Ordinal)
                .ToList();
            return _tasks;
        }
    }
}
