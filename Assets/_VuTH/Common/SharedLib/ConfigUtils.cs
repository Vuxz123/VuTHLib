namespace Common.SharedLib
{
    public static class ConfigUtils
    {
        public const string ConfigFolderPath = "_VuTH/Configs";

        public static string GetConfigPath(string configName)
        {
            return ConfigFolderPath + "/" + configName;
        }

#if UNITY_EDITOR
        public static string EnsureConfigFolderExists()
        {
            const string fullPath = "Assets/Resources/" + ConfigFolderPath;
            if (!UnityEditor.AssetDatabase.IsValidFolder(fullPath))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets/Resources/_VuTH", "Configs");
            }

            return fullPath;
        }
        
        public static string GetFullConfigAssetPath(string configName)
        {
            var folderPath = EnsureConfigFolderExists();
            return folderPath + "/" + configName + ".asset";
        }
#endif
    }
}