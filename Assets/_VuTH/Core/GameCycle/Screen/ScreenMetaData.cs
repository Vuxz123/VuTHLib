using System;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.LocalEvents;
using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen
{
    public class ScreenMetaData : MonoBehaviour, IScreenMetaData
    {
        [SerializeField] public string sceneName;
        [SerializeField] public ScreenModel model;

        public void OnValidate()
        {
            // Đảm bảo sceneName luôn đúng với scene hiện tại
            var currentScene = gameObject.scene;
            if (!currentScene.isLoaded) return;
            if (currentScene.IsValid())
            {
                if (!string.Equals(sceneName, currentScene.name, StringComparison.Ordinal))
                {
                    sceneName = currentScene.name;
                }
            }
            else
            {
                this.LogWarning($"[{currentScene.name}] Screen Meta Data đang nằm trong một scene không hợp lệ!");
            }
            
            // Đamt bảo Object MetaData luôn tồn tại là root object
            var root = transform.root;
            if (root != transform)
            {
                transform.SetParent(null);
            }
            
            // Đảm bảo Object MetaData luôn là GameObject root đầu tiên trong scene
            var rootObjects = currentScene.GetRootGameObjects();
            if (rootObjects.Length > 0)
            {
                if (rootObjects[0] != gameObject)
                {
                    this.LogError($"[{currentScene.name}] Screen Meta Data phải là GameObject root đầu tiên trong scene để dễ dàng tìm kiếm!");
                }
            }
            
            // Đảm bảo model không null
            if (!model)
            {
                this.LogError($"[{currentScene.name}] Screen Meta Data cần có Screen Model hợp lệ!");
            }
            
            // Đảm bảo object có tên là "ScreenMetaData"
            if (gameObject.name != "ScreenMetaData")
            {
                gameObject.name = "ScreenMetaData";
            }
        }

        public string SceneName => sceneName;
        public IScreenDefinition ScreenDefinition => model;
    }
}