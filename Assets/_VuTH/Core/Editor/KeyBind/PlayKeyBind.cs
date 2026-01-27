using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _VuTH.Core.Editor.KeyBind
{
    public static class PlayKeyBind
    {
        // %q tương đương với Ctrl + Q (Windows) hoặc Cmd + Q (macOS)
        [MenuItem("Tools/Play FrameWork %q")]
        public static void TogglePlayCore()
        {
            // 1. Nếu đang Play thì Stop
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            // 2. Hỏi người dùng có muốn lưu Scene hiện tại không trước khi chuyển
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return; // Nếu bấm Cancel thì dừng lại
            }

            // 3. Tìm Scene tên là "Core" trong project
            // Lệnh này tìm tất cả asset kiểu Scene có tên chứa chữ "Core"
            string[] guids = AssetDatabase.FindAssets("Core t:Scene");

            if (guids.Length == 0)
            {
                Debug.LogError("Cannot find Core scene!! There are corruptions or miss configurations!");
                return;
            }

            // Lấy đường dẫn của kết quả đầu tiên tìm được
            string scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);

            // (Tuỳ chọn) Nếu bạn có nhiều file tên Core, bạn có thể hardcode đường dẫn chính xác ở đây:
            // string scenePath = "Assets/Scenes/Core.unity"; 

            // 4. Mở Scene Core
            EditorSceneManager.OpenScene(scenePath);

            // 5. Bắt đầu Play Mode
            EditorApplication.isPlaying = true;
        }
    }
}