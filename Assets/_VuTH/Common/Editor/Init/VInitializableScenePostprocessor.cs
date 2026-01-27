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
            // 1. Lấy Scene Object
            var scene = SceneManager.GetSceneByPath(importedScenePath);
            
            // Nếu scene chưa load vào Editor, ta buộc phải mở nó để đọc Component
            // (OpenSceneMode.Additive để không đóng scene hiện tại của bạn)
            var wasLoaded = scene.isLoaded;
            if (!wasLoaded)
            {
                this.LogWarning("Scene not loaded");
                return;
            }
            try
            {
                // 2. Tìm tất cả các Initializable trong Scene
                var rootGameObjects = scene.GetRootGameObjects();
                var initializablesList = rootGameObjects.AsValueEnumerable().
                    SelectMany(o => o.DescendantsAndSelf()
                        .SelectMany( gameObject => gameObject.GetComponents<IVInitializable>())).ToList();
                
                if (initializablesList.Count == 0)
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
                
                // 4. Gán tất cả Initializable vào InvokeSite qua properties (hiện tại đang private)
                if (initializablesList.Count > 0)
                {
                    // Convert to array of IVInitializable
                    var arr = initializablesList.ToArray();
                    // Use the public helper on the invoke site to assign them
                    invokeSite.AssignInitializables(arr);

#if UNITY_EDITOR
                    // Mark the scene dirty so the created profile / invoke site will be saved
                    EditorSceneManager.MarkSceneDirty(scene);
#endif
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