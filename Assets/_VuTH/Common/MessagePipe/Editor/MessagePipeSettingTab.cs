using System.IO;
using System.Linq;
using _VuTH.Common.Editor.Settings;
using _VuTH.Common.Editor.Settings.Util;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Settings tab for MessagePipe: options asset + a read-only browser of the discovered <c>[MessagePipeEvent]</c>
    /// types. Nothing to bake — events are discovered at runtime (see <see cref="MessagePipeEventCatalog"/>).
    /// </summary>
    [SettingsTab]
    public class MessagePipeSettingTab : ISettingsTab
    {
        public string Id => "MessagePipe";
        public string Title => "MessagePipe";
        public int Order => 30;

        private VisualElement _optionsRoot;
        private VisualElement _eventsRoot;
        private VisualElement _validationRoot;

        public VisualElement CreateView()
        {
            var container = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            container.Add(new SettingTitle("MessagePipe Settings"));

            var how = new HelpBox(
                "Global events need no setup: any type can be published/subscribed through the root scope. " +
                "[MessagePipeEvent(EventScope.Scene)] gives every scene scope its own isolated broker (optionally only a " +
                "named scene); [MessagePipeEvent(EventScope.Screen, ScreenIds.Names.X)] does the same for every scene of a " +
                "screen. Resolving a scoped event elsewhere is reported (Options > Scoped Event Misuse).",
                HelpBoxMessageType.Info);
            container.Add(how);

            var options = new SettingSection("Options");
            _optionsRoot = new VisualElement();
            options.Add(_optionsRoot);
            container.Add(options);

            var validation = new SettingSection("Validation");
            _validationRoot = new VisualElement();
            validation.Add(_validationRoot);
            container.Add(validation);

            var events = new SettingSection("Discovered Events");
            var refresh = new Button(() =>
            {
                MessagePipeEventCatalog.Invalidate();
                RefreshEvents();
                RefreshValidation();
            }) { text = "Refresh", style = { alignSelf = Align.FlexStart, marginBottom = 4 } };
            events.Add(refresh);
            _eventsRoot = new VisualElement();
            events.Add(_eventsRoot);
            container.Add(events);

            RefreshOptions();
            RefreshValidation();
            RefreshEvents();
            return container;
        }

        private void RefreshOptions()
        {
            _optionsRoot.Clear();

            var config = AssetDatabase.LoadAssetAtPath<MessagePipeOptionsConfig>(MessagePipeConstants.AbsoluteOptionsConfigPath);
            if (!config)
            {
                _optionsRoot.Add(new HelpBox("No options asset: MessagePipe defaults are used.", HelpBoxMessageType.None));
                _optionsRoot.Add(new Button(() =>
                {
                    CreateConfigAsset();
                    RefreshOptions();
                }) { text = "Create Options Config", style = { alignSelf = Align.FlexStart } });
                return;
            }

            var serialized = new SerializedObject(config);
            var toggle = new PropertyField(serialized.FindProperty(nameof(MessagePipeOptionsConfig.enableCaptureStackTrace)),
                "Capture Stack Traces");
            toggle.Bind(serialized);
            _optionsRoot.Add(toggle);

            var misuse = new PropertyField(serialized.FindProperty(nameof(MessagePipeOptionsConfig.scopedEventMisuse)),
                "Scoped Event Misuse");
            misuse.Bind(serialized);
            _optionsRoot.Add(misuse);

            var assetField = new ObjectField("Asset") { objectType = typeof(MessagePipeOptionsConfig), value = config };
            assetField.SetEnabled(false);
            _optionsRoot.Add(assetField);
        }

        private void RefreshEvents()
        {
            _eventsRoot.Clear();

            var all = MessagePipeEventCatalog.All;
            if (all.Count == 0)
            {
                _eventsRoot.Add(new Label("No [MessagePipeEvent] types found."));
                return;
            }

            AddGroup("Global", all.Where(e => e.Scope == EventScope.Global));
            AddGroup("Scene — every scene", all.Where(e => e.Scope == EventScope.Scene && e.SceneName.Length == 0));
            foreach (var scene in all.Where(e => e.Scope == EventScope.Scene && e.SceneName.Length > 0)
                         .GroupBy(e => e.SceneName)
                         .OrderBy(g => g.Key, System.StringComparer.Ordinal))
            {
                AddGroup($"Scene — \"{scene.Key}\"", scene);
            }

            foreach (var screen in all.Where(e => e.Scope == EventScope.Screen)
                         .GroupBy(e => e.ScreenId)
                         .OrderBy(g => g.Key, System.StringComparer.Ordinal))
            {
                AddGroup($"Screen — \"{screen.Key}\"", screen);
            }
        }

        private void RefreshValidation()
        {
            _validationRoot.Clear();

            var issues = MessagePipeTargetValidator.ValidateProject();
            if (issues.Count == 0)
            {
                _validationRoot.Add(new HelpBox("All scene / screen targets exist.", HelpBoxMessageType.Info));
                return;
            }

            foreach (var issue in issues)
            {
                _validationRoot.Add(new HelpBox(issue.Message,
                    issue.IsError ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
            }
        }

        private void AddGroup(string title, System.Collections.Generic.IEnumerable<MessagePipeEventInfo> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return;

            var foldout = new Foldout { text = $"{title} ({list.Count})", value = true };
            foreach (var info in list)
            {
                foldout.Add(new Label(EventDisplayNameFormatter.Format(info.Type.AssemblyQualifiedName))
                {
                    tooltip = info.Type.AssemblyQualifiedName,
                    style = { marginLeft = 12 }
                });
            }

            _eventsRoot.Add(foldout);
        }

        private static void CreateConfigAsset()
        {
            var path = MessagePipeConstants.AbsoluteOptionsConfigPath;
            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MessagePipeOptionsConfig>(), path);
            AssetDatabase.SaveAssets();
        }
    }
}
