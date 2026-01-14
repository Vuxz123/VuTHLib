#if UNITY_EDITOR
using Common.SharedLib.Log;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Common.SharedLib.Scene
{
    public static class EditorSceneUtil
    {
        public static bool IsSceneOpen(AssetReference sceneRef)
        {
            if (sceneRef == null || !sceneRef.editorAsset)
                return false;

            var scenePath = AssetDatabase.GetAssetPath(sceneRef.editorAsset);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == scenePath)
                    return scene.isLoaded;
            }

            return false;
        }

        public static void OpenSceneInEditor(
            AssetReference sceneRef,
            OpenSceneMode mode
        )
        {
            if (sceneRef == null)
            {
                LogUtils.LogError("OpenSceneInEditor: AssetReference is null.");
                return;
            }

            if (sceneRef.editorAsset == null)
            {
                LogUtils.LogError($"OpenSceneInEditor: AssetReference '{sceneRef}' has no editorAsset.");
                return;
            }

            // Addressables Scene → editorAsset luôn là SceneAsset
            if (!(sceneRef.editorAsset is SceneAsset sceneAsset))
            {
                LogUtils.LogError($"OpenSceneInEditor: AssetReference '{sceneRef}' is not a SceneAsset.");
                return;
            }

            var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(scenePath))
            {
                LogUtils.LogError("OpenSceneInEditor: Cannot resolve scene path.");
                return;
            }

            // Nếu scene đã mở → focus và return
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var openedScene = SceneManager.GetSceneAt(i);
                if (openedScene.path != scenePath) continue;
                if (!openedScene.isLoaded) continue;
                SceneManager.SetActiveScene(openedScene);
                return;
            }

            // Nếu Single → hỏi save scene hiện tại (Unity standard behavior)
            if (mode == OpenSceneMode.Single)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
            }

            // Mở scene
            EditorSceneManager.OpenScene(scenePath, mode);
        }
    }
}
#endif