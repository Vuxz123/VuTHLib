using Common.Editor.Settings;
using Common.Editor.Settings.Util;
using Common.Log;
using Core.Window.Profile;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Core.Window.Editor
{
    [SettingsTab]
    public class WindowSettingTab : ISettingsTab
    {
        public string Id => "Window";
        public string Title => "Window";
        public int Order => 20;

        private SerializedObject _serializedProfile;

        public VisualElement CreateView()
        {
            var container = new VisualElement();
            
            // 1. Tìm và khởi tạo SerializedObject
            if (WindowProfileUtilities.TryGetProfile(out var p))
            {
                _serializedProfile = new SerializedObject(p);
            }
            else
            {
                this.Log("Can't find WindowProfile.");
                container.Add(new Label("Error: WindowProfile not found."));
                return container;
            }

            container.Add(new SettingTitle("Window System Settings"));

            // 2. Tạo các nhóm setting tương ứng với Header trong Profile
            container.Add(CreateCanvasSettingsSection());
            container.Add(CreateTransitionsSection());
            container.Add(CreateInputBlockingSection());
            container.Add(CreateMemorySection());
            container.Add(CreateDebugSection());
            
            // 3. Bind dữ liệu
            container.Bind(_serializedProfile);
            
            return container;
        }

        // --- Group 1: Canvas Settings ---
        private VisualElement CreateCanvasSettingsSection()
        {
            var section = new SettingSection("Canvas Settings");
            
            section.Add(new PropertyField(_serializedProfile.FindProperty("screenBaseSortingOrder")));
            section.Add(new PropertyField(_serializedProfile.FindProperty("popupBaseSortingOrder")));
            section.Add(new PropertyField(_serializedProfile.FindProperty("systemBaseSortingOrder")));
            section.Add(new PropertyField(_serializedProfile.FindProperty("sortingOrderStep")));

            return section;
        }

        // --- Group 2: Transitions ---
        private VisualElement CreateTransitionsSection()
        {
            var section = new SettingSection("Transitions");

            section.Add(new PropertyField(_serializedProfile.FindProperty("defaultTransitionDuration")));
            section.Add(new PropertyField(_serializedProfile.FindProperty("useTransitionsInEditor")));

            return section;
        }

        // --- Group 3: Input Blocking ---
        private VisualElement CreateInputBlockingSection()
        {
            var section = new SettingSection("Input Blocking");

            section.Add(new PropertyField(_serializedProfile.FindProperty("blockInputDuringTransitions")));
            section.Add(new PropertyField(_serializedProfile.FindProperty("minBlockDuration")));

            return section;
        }

        // --- Group 4: Memory Management ---
        private VisualElement CreateMemorySection()
        {
            var section = new SettingSection("Memory Management");

            section.Add(new PropertyField(_serializedProfile.FindProperty("maxCachedWindows")));
            section.Add(new PropertyField(_serializedProfile.FindProperty("windowCacheTimeout")));

            return section;
        }

        // --- Group 5: Debug ---
        private VisualElement CreateDebugSection()
        {
            var section = new SettingSection("Debug");

            section.Add(new PropertyField(_serializedProfile.FindProperty("enableDebugLogs")));
            section.Add(new PropertyField(_serializedProfile.FindProperty("showUIStackInHierarchy")));

            return section;
        }
    }
}