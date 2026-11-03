using _VuTH.Common.Log;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace _VuTH.Common.Editor.Scene
{
    /// <summary>Editor helpers for scenes referenced through Addressables <see cref="AssetReference"/>s.</summary>
    public static class EditorSceneUtil
    {
        /// <summary>True when the scene behind <paramref name="sceneRef"/> is currently open (loaded) in the editor.</summary>
        public static bool IsSceneOpen(AssetReference sceneRef)
        {
            if (sceneRef == null || !sceneRef.editorAsset) return false;

            var scenePath = AssetDatabase.GetAssetPath(sceneRef.editorAsset);
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == scenePath) return scene.isLoaded;
            }

            return false;
        }

        /// <summary>
        /// Opens the scene behind <paramref name="sceneRef"/>. If it is already open it only becomes the active scene.
        /// With <see cref="OpenSceneMode.Single"/> the user is first asked to save modified scenes (Unity's standard
        /// behaviour); cancelling aborts.
        /// </summary>
        public static void OpenSceneInEditor(AssetReference sceneRef, OpenSceneMode mode)
        {
            if (sceneRef == null)
            {
                typeof(EditorSceneUtil).LogError("OpenSceneInEditor: AssetReference is null.");
                return;
            }

            if (!sceneRef.editorAsset)
            {
                typeof(EditorSceneUtil).LogError($"OpenSceneInEditor: AssetReference '{sceneRef}' has no editorAsset.");
                return;
            }

            // An Addressables scene reference always points at a SceneAsset.
            if (sceneRef.editorAsset is not SceneAsset sceneAsset)
            {
                typeof(EditorSceneUtil).LogError($"OpenSceneInEditor: AssetReference '{sceneRef}' is not a SceneAsset.");
                return;
            }

            var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(scenePath))
            {
                typeof(EditorSceneUtil).LogError("OpenSceneInEditor: Cannot resolve scene path.");
                return;
            }

            // Already open: just focus it.
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var openedScene = SceneManager.GetSceneAt(i);
                if (openedScene.path != scenePath || !openedScene.isLoaded) continue;

                SceneManager.SetActiveScene(openedScene);
                return;
            }

            if (mode == OpenSceneMode.Single && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(scenePath, mode);
        }
    }
}
