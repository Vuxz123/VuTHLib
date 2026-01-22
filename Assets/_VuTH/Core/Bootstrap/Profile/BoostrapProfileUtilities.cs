using System.IO;
using Common.Log;
using Core.GameCycle.ScreenFlow;
using Core.GameCycle.ScreenFlow.Profile;
using UnityEditor;
using UnityEngine;

namespace Core.Bootstrap.Profile
{
    public static class BootstrapProfileUtilities
    {
        public static bool TryGetProfile(out BootstrapProfile profile)
        {
            profile = null;

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                // Runtime logic (giữ nguyên nếu cần dùng trong game)
                profile = Resources.Load<BootstrapProfile>(BootstrapManagerCentralConst.BootstrapProfilePath);
                return profile != null;
            }
            
            // 1. Dùng AssetDatabase để load chính xác theo đường dẫn tuyệt đối (Assets/...)
            // Đảm bảo BootstrapManagerCentralConst.AbsoluteBootstrapProfilePath bắt đầu bằng "Assets/..." và có đuôi ".asset"
            profile = AssetDatabase.LoadAssetAtPath<BootstrapProfile>(BootstrapManagerCentralConst.AbsoluteBootstrapProfilePath);

            // 2. Nếu tìm thấy thì trả về luôn
            if (profile) return true;

            // 3. Nếu chưa có thì tạo mới
            typeof(BootstrapProfileUtilities).LogWarning($"Creating new profile at: {BootstrapManagerCentralConst.AbsoluteBootstrapProfilePath}");
            
            EnsureFolderExists(BootstrapManagerCentralConst.AbsoluteBootstrapProfilePath);
            
            profile = ScriptableObject.CreateInstance<BootstrapProfile>();
            AssetDatabase.CreateAsset(profile, BootstrapManagerCentralConst.AbsoluteBootstrapProfilePath);
            AssetDatabase.SaveAssets(); // Lưu ngay lập tức
            
            return true;
#else
            // Runtime logic (giữ nguyên nếu cần dùng trong game)
            profile = Resources.Load<BootstrapProfile>(BootstrapManagerCentralConst.ProfilePath);
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