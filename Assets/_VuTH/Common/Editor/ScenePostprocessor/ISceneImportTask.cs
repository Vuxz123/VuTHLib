namespace _VuTH.Common.Editor.ScenePostprocessor
{
    /// <summary>
    /// A step run by <see cref="ScenePostprocessor"/> on a scene right before it is saved.
    /// Implementations need a public parameterless constructor; they are discovered automatically.
    /// </summary>
    public interface ISceneImportTask
    {
        /// <summary>Execution order (lower runs first).</summary>
        int Order { get; }

        /// <summary>Legacy entry point: the path of the scene being saved.</summary>
        void OnSceneImported(string importedScenePath);

        /// <summary>
        /// Called with the scene being saved. Prefer this one: a scene saved for the first time has no path yet, so it
        /// cannot be found by path. Defaults to <see cref="OnSceneImported"/>.
        /// </summary>
        void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => OnSceneImported(path);
    }
}
