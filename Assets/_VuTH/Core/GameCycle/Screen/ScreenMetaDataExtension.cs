using Common.SharedLib.Log;
using UnityEngine;

namespace Core.GameCycle.Screen
{
    public static class ScreenMetaDataExtension
    {
        public static ScreenMetaData GetScreenMetaData(this GameObject gameObject)
        {
            var currentScene = gameObject.scene;

            foreach (var rootGameObject in currentScene.GetRootGameObjects())
            {
                if (rootGameObject.name != "ScreenMetaData") continue;
                var screenMetaData = rootGameObject.GetComponent<ScreenMetaData>();
                if (screenMetaData)
                {
                    return screenMetaData;
                }
                gameObject.LogError($"ScreenMetaData GameObject found in scene '{currentScene.name}' but it does not have a ScreenMetaData component attached.");
                return null;
            }
            
            gameObject.LogError($"No ScreenMetaData GameObject found in scene '{currentScene.name}'.");
            return null;
        }
    }
}