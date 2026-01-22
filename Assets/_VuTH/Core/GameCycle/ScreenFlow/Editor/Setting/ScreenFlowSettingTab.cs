using Common.Editor.Settings;
using Common.Editor.Settings.Util;
using Common.Log;
using Core.GameCycle.ScreenFlow.Editor.Validator;
using Core.GameCycle.ScreenFlow.Profile;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using ZLinq;

namespace Core.GameCycle.ScreenFlow.Editor.Setting
{
    [SettingsTab]
    public class ScreenFlowSettingTab : ISettingsTab
    {
        public string Id => "ScreenFlow";
        public string Title => "Screen Flow";
        public int Order => 20;

        // 1. Đưa SerializedObject ra ngoài để nó sống cùng vòng đời của Tab
        private SerializedObject _serializedProfile;

        public VisualElement CreateView()
        {
            var container = new VisualElement();
            
            // Khởi tạo SerializedObject MỘT LẦN DUY NHẤT
            if (ScreenFlowProfileUtilities.TryGetProfile(out var p))
            {
                _serializedProfile = new SerializedObject(p);
            }
            else
            {
                this.Log("Can't find profile.");
                container.Add(new Label("Error: Profile not found."));
                return container;
            }

            container.Add(new SettingTitle("Screen Flow Settings"));
            container.Add(CreateRootFlowSection());
            container.Add(CreateValidationSection());
            container.Add(CreateInfoSection());

            return container;
        }

        // =========================================================
        // Root Flow
        // =========================================================
        private VisualElement CreateRootFlowSection()
        {
            var section = new SettingSection("Root Flow");

            // Sử dụng _serializedProfile đã tạo ở trên
            var graphProp = _serializedProfile.FindProperty("graph");
            
            // ObjectField
            var graphField = new ObjectField("ScreenFlowGraph")
            {
                objectType = typeof(ScreenFlowGraph),
                allowSceneObjects = false
            };
            
            // Binding giờ sẽ hoạt động tốt vì _serializedProfile tồn tại vĩnh viễn
            graphField.BindProperty(graphProp);

            section.Add(graphField);

            // Action buttons
            var buttonRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginTop = 4 }
            };
            
            // 2. Sửa logic nút bấm:
            // KHÔNG lưu giá trị ra biến tạm thời.
            // Truy cập trực tiếp vào property bên trong sự kiện click để lấy giá trị MỚI NHẤT.
            
            var pingBtn = new Button(() =>
            {
                // Lấy giá trị hiện tại real-time
                var currentGraph = graphProp.objectReferenceValue as ScreenFlowGraph;
                if (!currentGraph) return;
                EditorGUIUtility.PingObject(currentGraph);
                Selection.activeObject = currentGraph;
            })
            {
                text = "Ping Asset"
            };

            var openBtn = new Button(() => 
            { 
                var currentGraph = graphProp.objectReferenceValue as ScreenFlowGraph;
                if (currentGraph)
                {
                    ScreenFlowGraphEditorWindow.Open(currentGraph); 
                }
            })
            {
                text = "Open Graph"
            };

            buttonRow.Add(pingBtn);
            buttonRow.Add(openBtn);

            section.Add(buttonRow);
            return section;
        }

        // =========================================================
        // Validation
        // =========================================================
        private VisualElement CreateValidationSection()
        {
            var section = new SettingSection("Validation");

            if (_serializedProfile == null) return section;

            // Cập nhật giá trị mới nhất trước khi validate
            _serializedProfile.Update();
            
            var graph = _serializedProfile.FindProperty("graph").objectReferenceValue as ScreenFlowGraph;
            
            if (!graph) return section;

            var report = ScreenFlowValidator.Validate(graph);

            if (report.Count == 0)
            {
                section.Add(new HelpBox("Graph is valid.", HelpBoxMessageType.Info));
                return section;
            }

            foreach (var error in report.AsValueEnumerable().Where(v => v.Severity == ScreenFlowValidationSeverity.Error))
            {
                section.Add(new HelpBox(error.Message, HelpBoxMessageType.Error));
            }

            foreach (var warning in report.AsValueEnumerable().Where(v => v.Severity == ScreenFlowValidationSeverity.Warning))
            {
                section.Add(new HelpBox(warning.Message, HelpBoxMessageType.Warning));
            }

            return section;
        }

        //Info Section giữ nguyên
        private static VisualElement CreateInfoSection()
        {
            var section = new SettingSection("Info");
            section.Add(new Label("• Single root flow enforced"));
            section.Add(new Label("• Runtime flow is immutable"));
            section.Add(new Label("• Flow cannot be swapped or reloaded"));
            return section;
        }
    }
}