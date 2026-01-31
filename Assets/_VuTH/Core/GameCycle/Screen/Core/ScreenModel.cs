using System;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Identifier;
using _VuTH.Core.GameCycle.Screen.Loading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace _VuTH.Core.GameCycle.Screen.Core
{
    [Serializable]
    public struct AdditiveSceneAddressableData
    {
        public AssetReference sceneRef;
        public bool unloadOnClose;
    }

    [CreateAssetMenu(fileName = "New Screen Model", menuName = "Screen/Screen Model")]
    public class ScreenModel : ScriptableObject, IScreenDefinition
    {
        [Header("Identifier")]
        [Tooltip("Định danh duy nhất cho màn hình này")]
        public ScreenIdentifier screenId;

        [Header("Scene (Addressables)")]
        [Tooltip("Scene chính (Addressables). ScreenManager sẽ quyết định load mode theo Enter/Push")]
        public AssetReference mainSceneRef;

        [Tooltip("Các scene phụ tải kèm (UI, Logic,...) theo Addressables")]
        public AdditiveSceneAddressableData[] additiveScene;

        [Header("Cache")]
        [Tooltip("Nếu true: khi đóng Screen sẽ chỉ SetActive(false) root GameObjects, không Unload scene.")]
        public bool softCache = false;

        [Header("Settings")]
        public bool showLoadingScreen = true;

        [Tooltip("Nhạc nền sẽ phát khi vào màn hình này (Optional)")]
        public AudioClip backgroundMusic;

        [Header("Loading Logic")]
        [Tooltip("Các task logic cần chạy xong mới được tắt Loading UI. Không phải Screen trong FlowGraph.")]
        public ScreenLoadingTask[] preloadingTasks;

        private void OnValidate()
        {
            if (!screenId)
            {
                this.LogError("Screen Model asset chưa có Screen Identifier! Vui lòng gán Screen Identifier phù hợp.");
                return;
            }

            if (string.IsNullOrEmpty(name) || name == "New Screen Model")
            {
                this.LogError($"Screen Model asset với id {screenId} chưa có tên hợp lệ! Vui lòng đổi tên asset cho phù hợp.");
            }

            // Optional validation: mainSceneRef can be empty for overlay-only screens
        }

        public ScreenIdentifier ScreenID => screenId;
    }
}