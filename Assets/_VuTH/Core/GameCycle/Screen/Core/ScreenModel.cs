using System;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Core;
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
        public bool softCache;

        [Header("Settings")]
        public bool showLoadingScreen = true;

        [Tooltip("Khi Push màn này: ẩn (SetActive false root objects) màn ngay bên dưới cho tới khi Pop màn này. " +
                 "Màn bên dưới không bị unload. Có thể ghi đè theo từng lần gọi Push(screen, hideScreenBelow, context).")]
        public bool hideScreenBelow;

        [Tooltip("Nhạc nền sẽ phát khi vào màn hình này (Optional). Để trống = giữ nguyên nhạc đang phát " +
                 "(trừ khi bật Stop Music If No Background Music). AudioManager có thể ghi đè bằng AudioCue theo màn.")]
        public AudioClip backgroundMusic;

        [Tooltip("Chỉ áp dụng khi Background Music để trống: tắt nhạc đang phát khi vào màn này. " +
                 "Mặc định tắt = giữ nhạc (phù hợp cho popup / màn được Push lên).")]
        public bool stopMusicIfNoBackgroundMusic;

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