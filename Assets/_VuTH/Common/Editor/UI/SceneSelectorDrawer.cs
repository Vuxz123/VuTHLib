using System.Collections.Generic;
using System.IO;
using Common.Scene;
using UnityEditor;
using UnityEngine;

namespace Common.Editor.UI
{
    [CustomPropertyDrawer(typeof(SceneSelectorAttribute))]
    public class SceneSelectorDrawer : PropertyDrawer
    {
        // Cache danh sách để hiển thị (GUIContent nhẹ hơn string khi vẽ nhiều)
        private static GUIContent[] _displayOptions;

        // Cache danh sách giá trị thực (tên scene)
        private static string[] _sceneValues;

        // Cờ kiểm tra xem đã đăng ký sự kiện chưa
        private static bool _isListenerRegistered;

        // Hàm dựng danh sách (Chỉ chạy khi cần thiết)
        private static void UpdateSceneList()
        {
            var scenesInBuild = EditorBuildSettings.scenes;

            // Tạo buffer tạm
            var optionsList = new List<GUIContent>();
            var valuesList = new List<string>();

            // Thêm option None
            optionsList.Add(new GUIContent("- None -"));
            valuesList.Add("");

            foreach (var scene in scenesInBuild)
            {
                if (!scene.enabled) continue;
                var name = Path.GetFileNameWithoutExtension(scene.path);
                optionsList.Add(new GUIContent(name)); // Tên hiển thị
                valuesList.Add(name); // Giá trị lưu xuống biến
            }

            _displayOptions = optionsList.ToArray();
            _sceneValues = valuesList.ToArray();
        }

        // Callback khi Build Settings thay đổi
        private static void OnBuildSettingsChanged()
        {
            // Invalidate cache (đánh dấu là dữ liệu cũ)
            _displayOptions = null;
            _sceneValues = null;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [SceneSelect] with strings.");
                return;
            }

            // 1. Đăng ký lắng nghe sự kiện (Chỉ làm 1 lần duy nhất)
            if (!_isListenerRegistered)
            {
                EditorBuildSettings.sceneListChanged += OnBuildSettingsChanged;
                _isListenerRegistered = true;
            }

            // 2. Nếu cache chưa có hoặc bị hủy do sự kiện thay đổi -> Build lại
            if (_displayOptions == null)
            {
                UpdateSceneList();
            }

            // 3. Tìm index hiện tại
            var currentName = property.stringValue;
            var currentIndex = 0;

            // Tìm trong cache xem có scene này không
            var foundIndex = System.Array.IndexOf(_sceneValues, currentName);

            if (foundIndex >= 0)
            {
                currentIndex = foundIndex;
            }
            else if (!string.IsNullOrEmpty(currentName))
            {
                // Trường hợp scene đang lưu không còn trong Build Settings
                // Ta vẽ tạm một cái Dropdown đặc biệt cho trường hợp này
                DrawMissingSceneWarning(position, property, label, currentName);
                return;
            }

            // 4. Vẽ Popup tối ưu
            EditorGUI.BeginChangeCheck();

            // Popup dùng int index và mảng GUIContent cực nhẹ
            var newIndex = EditorGUI.Popup(position, label, currentIndex, _displayOptions);

            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = _sceneValues[newIndex];
            }
        }

        // Tách riêng hàm xử lý khi scene bị thiếu để code chính gọn gàng
        private static void DrawMissingSceneWarning(
            Rect position, 
            SerializedProperty property, 
            GUIContent label,
            string currentName)
        {
            // Chia Rect thành 2 phần: Label và Button
            var labelWidth = EditorGUIUtility.labelWidth;
            var labelRect = new Rect(position.x, position.y, labelWidth, position.height);
            var contentRect = new Rect(position.x + labelWidth, position.y, position.width - labelWidth,
                position.height);

            EditorGUI.LabelField(labelRect, label);

            // Vẽ nút màu đỏ cảnh báo
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // Đỏ nhạt

            if (GUI.Button(contentRect, $"{currentName} (Missing!)"))
            {
                // Nếu bấm vào thì reset về rỗng (hoặc hiện menu chọn lại - tùy bạn)
                property.stringValue = "";
                GUI.FocusControl(null); // Bỏ focus
            }

            GUI.backgroundColor = originalColor;
        }
    }
}