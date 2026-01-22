using System.IO;
using Common.Log;
using UnityEditor;
using UnityEngine;

namespace Core.Window.Profile
{
    public static class WindowProfileUtilities
    {
        public static bool TryGetProfile(out WindowProfile profile)
        {
            profile = null;

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                // Runtime logic (giữ nguyên nếu cần dùng trong game)
                profile = Resources.Load<WindowProfile>(WindowManagerConst.WindowProfilePath);
                return profile;
            }
            
            // 1. Dùng AssetDatabase để load chính xác theo đường dẫn tuyệt đối (Assets/...)
            // Đảm bảo WindowManagerConst.AbsoluteWindowProfilePath bắt đầu bằng "Assets/..." và có đuôi ".asset"
            profile = AssetDatabase.LoadAssetAtPath<WindowProfile>(WindowManagerConst.AbsoluteWindowProfilePath);

            // 2. Nếu tìm thấy thì trả về luôn
            if (profile) return true;

            // 3. Nếu chưa có thì tạo mới
            typeof(WindowProfileUtilities).LogWarning($"Creating new profile at: {WindowManagerConst.AbsoluteWindowProfilePath}");
            
            EnsureFolderExists(WindowManagerConst.AbsoluteWindowProfilePath);
            
            profile = ScriptableObject.CreateInstance<WindowProfile>();
            AssetDatabase.CreateAsset(profile, WindowManagerConst.AbsoluteWindowProfilePath);
            AssetDatabase.SaveAssets(); // Lưu ngay lập tức
            
            return true;
#else
            // Runtime logic (giữ nguyên nếu cần dùng trong game)
            profile = Resources.Load<WindowProfile>(WindowManagerConst.ProfilePath);
            return profile != null;
#endif
        }

#if UNITY_EDITOR
        private static void EnsureFolderExists(string assetPath)
        {
            var folderPath = Path.GetDirectoryName(assetPath);
            if (Directory.Exists(folderPath)) return;
            if (folderPath != null) Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
        }
#endif
    }
}