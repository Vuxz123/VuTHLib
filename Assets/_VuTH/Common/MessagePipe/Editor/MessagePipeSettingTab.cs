using _VuTH.Common.Editor.Settings;
using _VuTH.Common.Editor.Settings.Util;
using _VuTH.Common.MessagePipe.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Settings tab for MessagePipe. Builds the Status and Actions sections itself and delegates the
    /// Options, Assembly Whitelist and Events Browser sections to dedicated section views.
    /// </summary>
    [SettingsTab]
    public class MessagePipeSettingTab : ISettingsTab
    {
        public string Id => "MessagePipe";
        public string Title => "MessagePipe";
        public int Order => 30;

        private Label _lookupStatusLabel;
        private Label _registrarStatusLabel;
        private bool _loggedEventsBrowserBeforeInit;

        private MessagePipeOptionsSection _optionsSection;
        private AssemblyWhitelistSection _whitelistSection;
        private EventsBrowserSection _eventsBrowserSection;

        public VisualElement CreateView()
        {
            var container = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1
                }
            };

            // NOTE:
            // `CreateStatusSection()` calls `RefreshStatus()`, which calls `RefreshEventsBrowser()`.
            // Ensure the events browser section exists before status refresh during initial UI build.
            _eventsBrowserSection = new EventsBrowserSection();
            _loggedEventsBrowserBeforeInit = false;

            container.Add(new SettingTitle("MessagePipe Settings"));
            container.Add(CreateStatusSection());
            container.Add(CreateActionsSection());
            container.Add(CreateOptionsSection());
            container.Add(CreateWhitelistSection());
            container.Add(CreateEventsBrowserSection());

            return container;
        }

        private VisualElement CreateStatusSection()
        {
            var section = new SettingSection("Status");

            var lookupStatusRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            var lookupLabel = new Label("EventScopeLookup: ") { style = { width = 140 } };
            _lookupStatusLabel = new Label("Checking...") { style = { color = Color.gray } };
            lookupStatusRow.Add(lookupLabel);
            lookupStatusRow.Add(_lookupStatusLabel);
            section.Add(lookupStatusRow);

            var registrarStatusRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            var registrarLabel = new Label("MessagePipeRegistrar: ") { style = { width = 140 } };
            _registrarStatusLabel = new Label("Checking...") { style = { color = Color.gray } };
            registrarStatusRow.Add(registrarLabel);
            registrarStatusRow.Add(_registrarStatusLabel);
            section.Add(registrarStatusRow);

            RefreshStatus();

            return section;
        }

        private static VisualElement CreateActionsSection()
        {
            var section = new SettingSection("Actions");

            var buttonContainer = new VisualElement { style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center
            } };

            var bakeButton = new Button(MessagePipeEventBaker.BakeEventScopeLookup)
            {
                text = "Bake / Generate",
                style = { width = 150 }
            };
            buttonContainer.Add(bakeButton);

            var validateButton = new Button(MessagePipeEventBaker.ValidateBake)
            {
                text = "Validate",
                style = { width = 100 }
            };
            buttonContainer.Add(validateButton);

            var clearButton = new Button(MessagePipeEventBaker.ClearBaked)
            {
                text = "Clear Baked",
                style = { width = 120 }
            };
            buttonContainer.Add(clearButton);

            section.Add(buttonContainer);

            return section;
        }

        private VisualElement CreateOptionsSection()
        {
            var section = new SettingSection("MessagePipe Options");

            _optionsSection = new MessagePipeOptionsSection();
            _optionsSection.Refresh();

            section.Add(_optionsSection.Root);

            return section;
        }

        private VisualElement CreateWhitelistSection()
        {
            var section = new SettingSection("Assembly Whitelist");

            _whitelistSection = new AssemblyWhitelistSection();
            _whitelistSection.Refresh();

            section.Add(_whitelistSection.Root);

            return section;
        }

        private VisualElement CreateEventsBrowserSection()
        {
            var section = new SettingSection("Events Browser");

            // Section is created in `CreateView()` so status refresh can safely populate it.
            _eventsBrowserSection ??= new EventsBrowserSection();
            RefreshEventsBrowser();

            section.Add(_eventsBrowserSection.Root);

            return section;
        }

        private void RefreshEventsBrowser()
        {
            if (_eventsBrowserSection == null)
            {
                // Should not happen after `CreateView()` pre-initializes the section.
                // Keep a one-time log to validate call order if this regresses.
                if (_loggedEventsBrowserBeforeInit) return;
                Debug.LogWarning("[MessagePipe Settings] RefreshEventsBrowser called before _eventsBrowserContainer was initialized.");
                _loggedEventsBrowserBeforeInit = true;
                return;
            }

            _eventsBrowserSection.Refresh();
        }

        private void RefreshStatus()
        {
            // Check EventScopeLookup
            var lookupExists = AssetDatabase.LoadMainAssetAtPath(MessagePipeConstants.AbsoluteEventScopeLookupPath) != null;
            _lookupStatusLabel.text = lookupExists ? "✓ Generated" : "✗ Not Found";
            _lookupStatusLabel.style.color = lookupExists ? Color.green : Color.red;

            // Check MessagePipeRegistrar
            var registrarPath = "Assets/Game/Scripts/Generated/MessagePipeRegistrar.cs";
            var registrarExists = AssetDatabase.LoadAssetAtPath<Object>(registrarPath);
            _registrarStatusLabel.text = registrarExists ? "✓ Generated" : "✗ Not Found";
            _registrarStatusLabel.style.color = registrarExists ? Color.green : Color.red;

            // Also refresh the events browser when status changes
            RefreshEventsBrowser();
        }
    }
}
