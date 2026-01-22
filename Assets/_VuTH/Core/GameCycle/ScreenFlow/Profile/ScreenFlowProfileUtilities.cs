using System.IO;
using Common.Log;
using UnityEditor;
using UnityEngine;

namespace Core.GameCycle.ScreenFlow.Profile
{
    public static class ScreenFlowProfileUtilities
    {
        public static bool TryGetProfile(out ScreenFlowProfile profile)
        {
            profile = null;

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                // Khi chạy game trong Editor thì dùng Resources như runtime
                profile = Resources.Load<ScreenFlowProfile>(ScreenFlowConstants.ProfilePath);
                return profile != null;
            }
            
            // 1. Dùng AssetDatabase để load chính xác theo đường dẫn tuyệt đối (Assets/...)
            // Đảm bảo ScreenFlowConstants.AbsoluteProfilePath bắt đầu bằng "Assets/..." và có đuôi ".asset"
            profile = AssetDatabase.LoadAssetAtPath<ScreenFlowProfile>(ScreenFlowConstants.AbsoluteProfilePath);

            // 2. Nếu tìm thấy thì trả về luôn
            if (profile) return true;

            // 3. Nếu chưa có thì tạo mới
            typeof(ScreenFlowProfileUtilities).LogWarning($"Creating new profile at: {ScreenFlowConstants.AbsoluteProfilePath}");
            
            EnsureFolderExists(ScreenFlowConstants.AbsoluteProfilePath);
            
            profile = ScriptableObject.CreateInstance<ScreenFlowProfile>();
            AssetDatabase.CreateAsset(profile, ScreenFlowConstants.AbsoluteProfilePath);
            AssetDatabase.SaveAssets(); // Lưu ngay lập tức
            
            return true;
#else
            // Runtime logic (giữ nguyên nếu cần dùng trong game)
            profile = Resources.Load<ScreenFlowProfile>(ScreenFlowConstants.ProfilePath);
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