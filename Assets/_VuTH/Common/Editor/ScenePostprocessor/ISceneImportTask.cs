namespace _VuTH.Common.Editor.ScenePostprocessor
{
    public interface ISceneImportTask
    {
        // Trả về thứ tự ưu tiên chạy (số nhỏ chạy trước)
        int Order { get; }

        // Logic xử lý
        // model: ScreenModel bị ảnh hưởng
        // importedScenePath: Đường dẫn của Scene vừa bị thay đổi
        void OnSceneImported(string importedScenePath);
    }
}