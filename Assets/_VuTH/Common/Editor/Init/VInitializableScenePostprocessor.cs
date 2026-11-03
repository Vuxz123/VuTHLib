using System;
using _VuTH.Common.Editor.ScenePostprocessor;
using _VuTH.Common.Init;
using _VuTH.Common.Log;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZLinq;

namespace _VuTH.Common.Editor.Init
{
    public class VInitializableScenePostprocessorTask : ISceneImportTask
    {
        public int Order => 0;
        public void OnSceneImported(string importedScenePath)
        {
            Process(SceneManager.GetSceneByPath(importedScenePath), importedScenePath);
        }

        // Uses the Scene object directly: a scene saved for the first time has no path yet.
        public void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            Process(scene, path);
        }

        private void Process(UnityEngine.SceneManagement.Scene scene, string importedScenePath)
        {

            
            // Runs from EditorSceneManager.sceneSaving, so the scene is open; guard anyway.
            if (!scene.IsValid() || !scene.isLoaded) return;
            try
            {
                // 2. Tìm tất cả các Initializable trong Scene
                var rootGameObjects = scene.GetRootGameObjects();
                var initializablesList = rootGameObjects.AsValueEnumerable().
                    SelectMany(o => o.DescendantsAndSelf()
                        .SelectMany( gameObject => gameObject.GetComponents<IVInitializable>())).ToList();
                
                if (initializablesList.Count == 0 && !scene.TryGetVInitializeInvokeSite(out _))
                {
                    return;
                }
                
                // 3. Tìm invoke site, nếu ko có thì tạo mới
                VInitializeInvokeSite invokeSite = null;
                foreach (var go in rootGameObjects)
                {
                    invokeSite = go.GetComponentInChildren<VInitializeInvokeSite>(true);
                    if (invokeSite)
                        break;
                }
                
                if (!invokeSite)
                {
                    this.Log("Creating VInitializeInvokeSite");
                    var newGo = new GameObject("VInitializeInvokeSite");
                    invokeSite = newGo.AddComponent<VInitializeInvokeSite>();
                    // Đặt nó vào root của scene
                    SceneManager.MoveGameObjectToScene(newGo, scene);
                }
                
                // 4. Assign the scene's initializables. Only dirty the scene when something actually changed,
                //    otherwise every save would leave the scene dirty again.
                if (invokeSite.AssignInitializables(initializablesList.ToArray()))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                
            }
            catch (Exception e)
            {
                this.LogError($"Error processing scene {importedScenePath}: {e}");
                Debug.LogException(e);
            }
        }
    }
}