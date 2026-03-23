using _VuTH.Common.Editor.Settings;
using _VuTH.Common.Editor.Settings.Util;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.ScreenFlow.Editor.Validator;
using _VuTH.Core.GameCycle.ScreenFlow.Profile;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using ZLinq;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Setting
{
    [SettingsTab]
    public class ScreenFlowSettingTab : ISettingsTab
    {
        public string Id => "ScreenFlow";
        public string Title => "Screen Flow";
        public int Order => 20;

        private SerializedObject _serializedProfile;
        private SettingSection _validationSection;

        public VisualElement CreateView()
        {
            var container = new VisualElement();
            
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
            _validationSection = new SettingSection("Validation");
            container.Add(_validationSection);
            container.Add(CreateInfoSection());
            RefreshValidationSection();

            return container;
        }

        // =========================================================
        // Root Flow
        // =========================================================
        private VisualElement CreateRootFlowSection()
        {
            var section = new SettingSection("Root Flow");

            var graphProp = _serializedProfile.FindProperty("graph");
            
            var graphField = new ObjectField("ScreenFlowGraph")
            {
                objectType = typeof(ScreenFlowGraph),
                allowSceneObjects = false
            };
            
            graphField.BindProperty(graphProp);
            graphField.RegisterValueChangedCallback(_ =>
            {
                _serializedProfile.Update();
                _serializedProfile.ApplyModifiedProperties();
                RefreshValidationSection();
            });

            section.Add(graphField);

            var buttonRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginTop = 4 }
            };
            
            var pingBtn = new Button(() =>
            {
                _serializedProfile.Update();
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
                _serializedProfile.Update();
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
        private void RefreshValidationSection()
        {
            if (_validationSection == null) return;

            _validationSection.Clear();

            if (_serializedProfile == null) return;

            _serializedProfile.Update();
            
            var graph = _serializedProfile.FindProperty("graph").objectReferenceValue as ScreenFlowGraph;
            
            if (!graph)
            {
                _validationSection.Add(new HelpBox("No graph assigned to ScreenFlowProfile.", HelpBoxMessageType.Warning));
                return;
            }

            var report = ScreenFlowValidator.Validate(graph);

            if (report.Count == 0)
            {
                _validationSection.Add(new HelpBox("Graph is valid.", HelpBoxMessageType.Info));
                return;
            }

            foreach (var error in report.AsValueEnumerable().Where(v => v.Severity == ScreenFlowValidationSeverity.Error))
            {
                _validationSection.Add(new HelpBox(error.Message, HelpBoxMessageType.Error));
            }

            foreach (var warning in report.AsValueEnumerable().Where(v => v.Severity == ScreenFlowValidationSeverity.Warning))
            {
                _validationSection.Add(new HelpBox(warning.Message, HelpBoxMessageType.Warning));
            }
        }

        private static VisualElement CreateInfoSection()
        {
            var section = new SettingSection("Info");
            section.Add(new Label("• ScreenFlowProfile selects which ScreenFlowGraph bootstrap will load"));
            section.Add(new Label("• ScreenFlowManager reads that graph once during InitializeBootstrap"));
            section.Add(new Label("• Editing the profile in Editor does not hot-reload an already running flow"));
            return section;
        }
    }
}
