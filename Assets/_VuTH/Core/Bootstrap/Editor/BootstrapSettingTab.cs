using Common.Editor.Settings;
using Common.Editor.Settings.Util;
using Common.Log;
using Core.Bootstrap.Profile;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Core.Bootstrap.Editor
{
    [SettingsTab]
    public class BootstrapSettingTab : ISettingsTab
    {
        public string Id => "Bootstrap";
        public string Title => "Bootstrap";
        public int Order => 20;

        // Giữ SerializedObject để binding hoạt động ổn định
        private SerializedObject _serializedProfile;

        public VisualElement CreateView()
        {
            var container = new VisualElement();
            
            // 1. Tìm và khởi tạo SerializedObject
            if (BootstrapProfileUtilities.TryGetProfile(out var p))
            {
                _serializedProfile = new SerializedObject(p);
            }
            else
            {
                this.Log("Can't find profile.");
                container.Add(new Label("Error: BootstrapProfile not found."));
                return container;
            }

            container.Add(new SettingTitle("Bootstrap Settings"));
            container.Add(CreateRootFlowSection());
            
            // Quan trọng: Bind toàn bộ container vào SerializedObject để dữ liệu tự động sync
            container.Bind(_serializedProfile);
            
            return container;
        }

        private VisualElement CreateRootFlowSection()
        {
            var section = new SettingSection("Bootstrap Configuration");

            // Lấy property "boostrapPrefabs"
            var listProp = _serializedProfile.FindProperty("boostrapPrefabs");

            if (listProp != null)
            {
                // FIX: Dùng PropertyField thay cho ObjectField.
                // PropertyField sẽ tự động render giao diện List/Array chuẩn của Unity
                var propertyField = new PropertyField(listProp, "Bootstrap Prefabs");
                
                section.Add(propertyField);
            }
            else
            {
                section.Add(new Label("Error: Property 'boostrapPrefabs' not found."));
            }

            return section;
        }
    }
}