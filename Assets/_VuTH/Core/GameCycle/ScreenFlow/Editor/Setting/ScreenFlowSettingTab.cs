using Common.Editor.Settings;
using Common.Editor.Settings.Util;
using Core.GameCycle.ScreenFlow.Editor.Discovery;
using Core.GameCycle.ScreenFlow.Editor.Validator;
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

        public VisualElement CreateView()
        {
            var container = new VisualElement();
            // ─────────────────────────────────────────────
            // Title
            // ─────────────────────────────────────────────
            container.Add(new SettingTitle("Screen Flow Settings"));

            // ─────────────────────────────────────────────
            // Root Flow Section
            // ─────────────────────────────────────────────
            container.Add(CreateRootFlowSection());

            // ─────────────────────────────────────────────
            // Validation Section
            // ─────────────────────────────────────────────
            container.Add(CreateValidationSection());

            // ─────────────────────────────────────────────
            // Info Section
            // ─────────────────────────────────────────────
            container.Add(CreateInfoSection());

            return container;
        }

        // =========================================================
        // Root Flow
        // =========================================================
        private VisualElement CreateRootFlowSection()
        {
            var section = new SettingSection("Root Flow");

            var graph = ScreenFlowDiscovery.TryFindRootFlow(out var error);

            if (!graph)
            {
                section.Add(new HelpBox(
                    error ?? "No ScreenFlowGraph found.",
                    HelpBoxMessageType.Error
                ));
                return section;
            }

            // Read-only ObjectField
            var graphField = new ObjectField("ScreenFlowGraph")
            {
                value = graph,
                objectType = typeof(ScreenFlowGraph),
                allowSceneObjects = false
            };
            graphField.SetEnabled(false);

            section.Add(graphField);

            // Action buttons
            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 4
                }
            };

            var pingBtn = new Button(() =>
            {
                EditorGUIUtility.PingObject(graph);
                Selection.activeObject = graph;
            })
            {
                text = "Ping Asset"
            };

            var openBtn = new Button(() => { ScreenFlowGraphEditorWindow.Open(graph); })
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

            var graph = ScreenFlowDiscovery.TryFindRootFlow(out _);
            if (graph == null)
                return section;

            var report = ScreenFlowValidator.Validate(graph);

            if (report.Count == 0)
            {
                section.Add(new HelpBox(
                    "Graph is valid.",
                    HelpBoxMessageType.Info
                ));
                return section;
            }

            var errors = report.AsValueEnumerable()
                .Where(v => v.Severity == ScreenFlowValidationSeverity.Error);
            var warnings = report.AsValueEnumerable()
                .Where(v => v.Severity == ScreenFlowValidationSeverity.Warning);

            foreach (var error in errors)
            {
                section.Add(new HelpBox(error.Message, HelpBoxMessageType.Error));
            }

            foreach (var warning in warnings)
            {
                section.Add(new HelpBox(warning.Message, HelpBoxMessageType.Warning));
            }

            return section;
        }

        // =========================================================
        // Info
        // =========================================================
        private VisualElement CreateInfoSection()
        {
            var section = new SettingSection("Info");

            section.Add(new Label("• Single root flow enforced"));
            section.Add(new Label("• Runtime flow is immutable"));
            section.Add(new Label("• Flow cannot be swapped or reloaded"));

            return section;
        }
    }
}