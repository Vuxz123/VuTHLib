using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using _VuTH.Common.Editor.Settings;
using _VuTH.Common.Editor.Settings.Util;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;
using Object = UnityEngine.Object;

namespace _VuTH.Common.MessagePipe.Editor
{
    [SettingsTab]
    public class MessagePipeSettingTab : ISettingsTab
    {
        public string Id => "MessagePipe";
        public string Title => "MessagePipe";
        public int Order => 30;

        private Label _lookupStatusLabel;
        private Label _registrarStatusLabel;
        private VisualElement _eventsBrowserContainer;
        private VisualElement _whitelistContainer;
        private TextField _newAssemblyTextField;
        private bool _loggedEventsBrowserBeforeInit;
        
        // Options config UI elements
        private VisualElement _optionsContainer;
        private Toggle _enableCaptureStackTraceToggle;
        private Toggle _preserveRegistrarToggle;
        private MessagePipeOptionsConfig _optionsConfig;

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
            // Ensure the events browser container exists before status refresh during initial UI build.
            _eventsBrowserContainer = new VisualElement();
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

            _optionsContainer = new VisualElement { style = { marginTop = 8 } };
            RefreshOptionsUI();

            section.Add(_optionsContainer);

            return section;
        }

        private void RefreshOptionsUI()
        {
            if (_optionsContainer == null) return;

            _optionsContainer.Clear();

            _optionsConfig = LoadOptionsConfig();
            if (_optionsConfig == null)
            {
                var missingCard = new VisualElement
                {
                    style =
                    {
                        backgroundColor = new Color(0.16f, 0.13f, 0.08f, 1f),
                        borderLeftColor = new Color(0.4f, 0.3f, 0.12f, 1f),
                        borderRightColor = new Color(0.4f, 0.3f, 0.12f, 1f),
                        borderTopColor = new Color(0.4f, 0.3f, 0.12f, 1f),
                        borderBottomColor = new Color(0.4f, 0.3f, 0.12f, 1f),
                        borderLeftWidth = 1,
                        borderRightWidth = 1,
                        borderTopWidth = 1,
                        borderBottomWidth = 1,
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 8,
                        paddingBottom = 8,
                        marginBottom = 8
                    }
                };

                var noteLabel = new Label("Options config asset not found.")
                {
                    style =
                    {
                        color = new Color(1f, 0.75f, 0.35f, 1f),
                        fontSize = 12,
                        marginBottom = 6,
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                };
                missingCard.Add(noteLabel);

                var hintLabel = new Label("Create it to enable runtime options (stack trace capture, registrar preserve).")
                {
                    style =
                    {
                        color = new Color(0.85f, 0.85f, 0.85f, 1f),
                        fontSize = 11,
                        whiteSpace = WhiteSpace.Normal,
                        marginBottom = 6
                    }
                };
                missingCard.Add(hintLabel);

                var createButton = new Button(CreateOptionsConfigAsset)
                {
                    text = "Create Options Config",
                    style = { width = 180 }
                };
                missingCard.Add(createButton);

                _optionsContainer.Add(missingCard);
                return;
            }

            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f),
                    borderLeftColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderRightColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderTopColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderBottomColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 8,
                    paddingBottom = 10,
                    marginBottom = 10
                }
            };

            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6
                }
            };

            var titleLabel = new Label("Runtime Options")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    flexGrow = 1
                }
            };
            header.Add(titleLabel);

            var badge = new Label("Live")
            {
                style =
                {
                    backgroundColor = new Color(0.24f, 0.48f, 0.3f, 1f),
                    color = Color.white,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    fontSize = 10,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    minWidth = 30
                }
            };
            header.Add(badge);

            card.Add(header);

            var descLabel = new Label("These options are read at runtime via MessagePipeHelper.GetConfiguredOptions().")
            {
                style =
                {
                    color = Color.gray,
                    fontSize = 11,
                    marginBottom = 6,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            card.Add(descLabel);

            // Enable Capture Stack Trace toggle
            var toggleRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 4,
                    paddingBottom = 4
                }
            };

            var toggleLabel = new Label("Enable Capture Stack Trace")
            {
                style = { fontSize = 12, flexGrow = 1 }
            };
            toggleRow.Add(toggleLabel);

            _enableCaptureStackTraceToggle = new Toggle
            {
                value = _optionsConfig.enableCaptureStackTrace,
                style = { width = 50 }
            };
            _enableCaptureStackTraceToggle.RegisterValueChangedCallback(OnOptionsToggleChanged);
            toggleRow.Add(_enableCaptureStackTraceToggle);

            card.Add(toggleRow);

            var toggleHint = new Label("For debugging; adds overhead to broker captures.")
            {
                style =
                {
                    color = Color.gray,
                    fontSize = 10,
                    marginLeft = 2,
                    marginBottom = 6
                }
            };
            card.Add(toggleHint);

            // Preserve Registrar toggle
            var preserveRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 4,
                    paddingBottom = 4
                }
            };

            var preserveLabel = new Label("Preserve Registrar")
            {
                style = { fontSize = 12, flexGrow = 1 }
            };
            preserveRow.Add(preserveLabel);

            _preserveRegistrarToggle = new Toggle
            {
                value = _optionsConfig.preserveRegistrar,
                style = { width = 50 }
            };
            _preserveRegistrarToggle.RegisterValueChangedCallback(OnPreserveRegistrarToggleChanged);
            preserveRow.Add(_preserveRegistrarToggle);

            card.Add(preserveRow);

            var preserveHint = new Label("Adds [Preserve] to generated registrar to avoid code stripping.")
            {
                style =
                {
                    color = Color.gray,
                    fontSize = 10,
                    marginLeft = 2
                }
            };
            card.Add(preserveHint);

            _optionsContainer.Add(card);
        }

        private MessagePipeOptionsConfig LoadOptionsConfig()
        {
            try
            {
                return AssetDatabase.LoadAssetAtPath<MessagePipeOptionsConfig>(MessagePipeConstants.AbsoluteOptionsConfigPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MessagePipe Settings] Failed to load options config: {ex.Message}");
                return null;
            }
        }

        private void CreateOptionsConfigAsset()
        {
            var config = LookupPersistence.LoadOrCreateOptionsConfig();
            if (config != null)
            {
                _optionsConfig = config;
                Debug.Log($"[MessagePipe Settings] Created options config asset at {MessagePipeConstants.AbsoluteOptionsConfigPath}");
                RefreshOptionsUI();
            }
            else
            {
                Debug.LogError("[MessagePipe Settings] Failed to create options config asset.");
            }
        }

        private void OnOptionsToggleChanged(ChangeEvent<bool> evt)
        {
            if (_optionsConfig == null || _enableCaptureStackTraceToggle == null) return;

            _optionsConfig.enableCaptureStackTrace = evt.newValue;
            EditorUtility.SetDirty(_optionsConfig);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MessagePipe Settings] EnableCaptureStackTrace set to {evt.newValue}");
        }

        private void OnPreserveRegistrarToggleChanged(ChangeEvent<bool> evt)
        {
            if (_optionsConfig == null || _preserveRegistrarToggle == null) return;

            _optionsConfig.preserveRegistrar = evt.newValue;
            EditorUtility.SetDirty(_optionsConfig);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MessagePipe Settings] PreserveRegistrar set to {evt.newValue}");
        }

        private VisualElement CreateWhitelistSection()
        {
            var section = new SettingSection("Assembly Whitelist");

            _whitelistContainer = new VisualElement { style = { marginTop = 8 } };
            RefreshWhitelistUI();

            section.Add(_whitelistContainer);

            return section;
        }

        private void RefreshWhitelistUI()
        {
            if (_whitelistContainer == null) return;

            _whitelistContainer.Clear();

            var whitelist = LoadWhitelist();
            if (!whitelist)
            {
                // Show missing asset UI with create option
                var noteLabel = new Label("Assembly whitelist asset not found.")
                {
                    style =
                    {
                        color = new Color(1f, 0.6f, 0f, 1f),
                        fontSize = 12,
                        marginBottom = 8
                    }
                };
                _whitelistContainer.Add(noteLabel);

                var createButton = new Button(CreateWhitelistAsset)
                {
                    text = "Create Whitelist Asset",
                    style = { width = 160 }
                };
                _whitelistContainer.Add(createButton);
                return;
            }

            var assemblyNames = whitelist.AssemblyNames;

            // Card container for consistent padding/border
            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f),
                    borderLeftColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderRightColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderTopColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderBottomColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 6,
                    marginBottom = 8
                }
            };

            // Header with title + badge
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6
                }
            };

            var titleLabel = new Label("Whitelist Assemblies")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    flexGrow = 1
                }
            };
            header.Add(titleLabel);

            var badge = new Label((assemblyNames?.Count ?? 0).ToString())
            {
                style =
                {
                    backgroundColor = new Color(0.24f, 0.42f, 0.64f, 1f),
                    color = Color.white,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginLeft = 4,
                    fontSize = 11,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    minWidth = 20
                }
            };
            header.Add(badge);

            card.Add(header);

            // ScrollView for the entries list with constrained height
            var scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    maxHeight = 220,
                    marginBottom = 8,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderRightColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderTopColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderBottomColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4
                }
            };

            if (assemblyNames == null || assemblyNames.Count == 0)
            {
                var emptyLabel = new Label("No assemblies in whitelist.")
                {
                    style =
                    {
                        color = Color.gray,
                        fontSize = 11,
                        marginTop = 6,
                        marginBottom = 6,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                var emptyBox = new VisualElement
                {
                    style =
                    {
                        backgroundColor = new Color(0.19f, 0.19f, 0.19f, 1f),
                        paddingLeft = 6,
                        paddingRight = 6,
                        paddingTop = 6,
                        paddingBottom = 6,
                        marginBottom = 2
                    }
                };
                emptyBox.Add(emptyLabel);
                scrollView.Add(emptyBox);
            }
            else
            {
                // List existing entries with delete buttons
                for (var i = 0; i < assemblyNames.Count; i++)
                {
                    var row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            marginBottom = 2,
                            paddingLeft = 6,
                            paddingRight = 4,
                            paddingTop = 4,
                            paddingBottom = 4,
                            backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f)
                        }
                    };

                    var nameLabel = new Label(assemblyNames[i])
                    {
                        style = { fontSize = 12, flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft }
                    };
                    row.Add(nameLabel);

                    var i1 = i;
                    var deleteButton = new Button(() => DeleteAssemblyEntry(i1))
                    {
                        text = "✕",
                        style =
                        {
                            width = 22,
                            height = 18,
                            fontSize = 10,
                            marginLeft = 6,
                            unityTextAlign = TextAnchor.MiddleCenter
                        }
                    };
                    row.Add(deleteButton);

                    scrollView.Add(row);
                }
            }

            card.Add(scrollView);
            _whitelistContainer.Add(card);

            // Add new entry controls (outside scroll view)
            var addRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 4,
                    paddingLeft = 2,
                    paddingRight = 2
                }
            };

            var assemblyLabel = new Label("Add assembly:") { style = { fontSize = 11, marginBottom = 2 } };
            addRow.Add(assemblyLabel);

            _newAssemblyTextField = new TextField
            {
                tooltip = "Enter assembly name without .dll extension (e.g., MyGameplay)",
                style = { flexGrow = 1, fontSize = 12, marginLeft = 6 }
            };
            addRow.Add(_newAssemblyTextField);

            var addButton = new Button(AddNewAssembly)
            {
                text = "Add",
                style = { width = 70, marginLeft = 8 }
            };
            addRow.Add(addButton);

            _whitelistContainer.Add(addRow);

            // Info label
            var infoLabel = new Label("Enter assembly name without .dll extension")
            {
                style =
                {
                    color = Color.gray,
                    fontSize = 10,
                    marginTop = 2
                }
            };
            _whitelistContainer.Add(infoLabel);
        }

        private MessagePipeAssemblyWhitelist LoadWhitelist()
        {
            try
            {
                return AssetDatabase.LoadAssetAtPath<MessagePipeAssemblyWhitelist>(MessagePipeConstants.AbsoluteWhitelistPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MessagePipe Settings] Failed to load whitelist: {ex.Message}");
                return null;
            }
        }

        private void CreateWhitelistAsset()
        {
            var whitelist = LookupPersistence.LoadOrCreateWhitelist();
            if (whitelist != null)
            {
                Debug.Log($"[MessagePipe Settings] Created whitelist asset at {MessagePipeConstants.AbsoluteWhitelistPath}");
                RefreshWhitelistUI();
            }
            else
            {
                Debug.LogError("[MessagePipe Settings] Failed to create whitelist asset.");
            }
        }

        private void AddNewAssembly()
        {
            if (_newAssemblyTextField == null) return;

            var assemblyName = _newAssemblyTextField.value?.Trim();
            if (string.IsNullOrEmpty(assemblyName))
            {
                Debug.LogWarning("[MessagePipe Settings] Cannot add empty assembly name.");
                return;
            }

            var whitelist = LoadWhitelist();
            if (whitelist == null)
            {
                // Create asset first if missing
                whitelist = LookupPersistence.LoadOrCreateWhitelist();
                if (whitelist == null)
                {
                    Debug.LogError("[MessagePipe Settings] Cannot add assembly: whitelist asset could not be created.");
                    return;
                }
            }

            // Use reflection to access the private field since there's no public setter
            var assemblyNamesField = typeof(MessagePipeAssemblyWhitelist).GetField("assemblyNames",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (assemblyNamesField != null)
            {
                var assemblyNames = (List<string>)assemblyNamesField.GetValue(whitelist);
                if (assemblyNames == null)
                {
                    assemblyNames = new List<string>();
                    assemblyNamesField.SetValue(whitelist, assemblyNames);
                }

                // Check for duplicates (case-insensitive)
                var exists = assemblyNames.Any(a => a.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    Debug.LogWarning($"[MessagePipe Settings] Assembly '{assemblyName}' already exists in whitelist.");
                    return;
                }

                assemblyNames.Add(assemblyName);

                EditorUtility.SetDirty(whitelist);
                AssetDatabase.SaveAssets();

                // Clear the text field and refresh UI
                _newAssemblyTextField.value = string.Empty;
                RefreshWhitelistUI();

                Debug.Log($"[MessagePipe Settings] Added assembly '{assemblyName}' to whitelist.");
            }
            else
            {
                Debug.LogError("[MessagePipe Settings] Could not access assemblyNames field via reflection.");
            }
        }

        private void DeleteAssemblyEntry(int index)
        {
            var whitelist = LoadWhitelist();
            if (whitelist == null)
            {
                Debug.LogWarning("[MessagePipe Settings] Cannot delete: whitelist asset not found.");
                return;
            }

            var assemblyNamesField = typeof(MessagePipeAssemblyWhitelist).GetField("assemblyNames",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (assemblyNamesField != null)
            {
                var assemblyNames = (List<string>)assemblyNamesField.GetValue(whitelist);
                if (assemblyNames == null || index < 0 || index >= assemblyNames.Count)
                {
                    Debug.LogWarning("[MessagePipe Settings] Invalid assembly index.");
                    return;
                }

                var removedName = assemblyNames[index];
                assemblyNames.RemoveAt(index);

                EditorUtility.SetDirty(whitelist);
                AssetDatabase.SaveAssets();

                RefreshWhitelistUI();

                Debug.Log($"[MessagePipe Settings] Removed assembly '{removedName}' from whitelist.");
            }
            else
            {
                Debug.LogError("[MessagePipe Settings] Could not access assemblyNames field via reflection.");
            }
        }

        private VisualElement CreateEventsBrowserSection()
        {
            var section = new SettingSection("Events Browser");

            // Container is created in `CreateView()` so status refresh can safely populate it.
            _eventsBrowserContainer ??= new VisualElement();
            RefreshEventsBrowser();

            section.Add(_eventsBrowserContainer);

            return section;
        }

        private void RefreshEventsBrowser()
        {
            if (_eventsBrowserContainer == null)
            {
                // Should not happen after `CreateView()` pre-initializes the container.
                // Keep a one-time log to validate call order if this regresses.
                if (_loggedEventsBrowserBeforeInit) return;
                Debug.LogWarning("[MessagePipe Settings] RefreshEventsBrowser called before _eventsBrowserContainer was initialized.");
                _loggedEventsBrowserBeforeInit = true;
                return;
            }

            _eventsBrowserContainer.Clear();

            var lookup = LoadEventScopeLookup();
            if (lookup == null)
            {
                var noteLabel = new Label("No baked data found. Click \"Bake / Generate\" to create lookup data.")
                {
                    style =
                    {
                        color = new Color(1f, 0.6f, 0f, 1f),
                        fontSize = 12,
                        marginTop = 8,
                        marginBottom = 8,
                        whiteSpace = WhiteSpace.Normal
                    }
                };
                _eventsBrowserContainer.Add(noteLabel);
                return;
            }

            var entries = lookup.Entries;
            if (entries == null || entries.Count == 0)
            {
                var noteLabel = new Label("No events found in baked data.")
                {
                    style =
                    {
                        color = Color.gray,
                        fontSize = 12,
                        marginTop = 8,
                        marginBottom = 8
                    }
                };
                _eventsBrowserContainer.Add(noteLabel);
                return;
            }

            var totalCount = entries.Count;
            // Separate global and scene events
            var globalEvents = new List<EventScopeEntry>();
            var sceneEventsByScene = new Dictionary<string, List<EventScopeEntry>>();

            foreach (var entry in entries)
            {
                if (entry.scope == EventScope.Global)
                {
                    globalEvents.Add(entry);
                }
                else // Scene
                {
                    var sceneName = string.IsNullOrEmpty(entry.sceneName) ? "(empty)" : entry.sceneName;
                    if (!sceneEventsByScene.TryGetValue(sceneName, out var list))
                    {
                        list = new List<EventScopeEntry>();
                        sceneEventsByScene[sceneName] = list;
                    }
                    list.Add(entry);
                }
            }

            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f),
                    borderLeftColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderRightColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderTopColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderBottomColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 8,
                    paddingBottom = 10,
                    marginBottom = 10
                }
            };

            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6
                }
            };

            var titleLabel = new Label("Baked Events")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    flexGrow = 1
                }
            };
            header.Add(titleLabel);

            var totalBadge = new Label(totalCount.ToString())
            {
                style =
                {
                    backgroundColor = new Color(0.24f, 0.42f, 0.64f, 1f),
                    color = Color.white,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    fontSize = 10,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginLeft = 4,
                    minWidth = 26
                }
            };
            header.Add(totalBadge);

            card.Add(header);

            var hint = new Label("Shows baked events grouped by scope and scene.")
            {
                style =
                {
                    color = Color.gray,
                    fontSize = 11,
                    marginBottom = 6
                }
            };
            card.Add(hint);

            var scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    maxHeight = 280,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderRightColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderTopColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderBottomColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4
                }
            };

            // Global Events foldout (expanded)
            var globalFoldout = new Foldout
            {
                text = $"Global Events ({globalEvents.Count})",
                value = true,
                style =
                {
                    marginTop = 4,
                    marginLeft = 4,
                    marginBottom = 6
                }
            };

            foreach (var eventLabel in globalEvents.AsValueEnumerable()
                         .Select(CreateEventLabel))
            {
                globalFoldout.Add(eventLabel);
            }

            if (globalEvents.Count == 0)
            {
                var emptyLabel = new Label("(none)") { style = { color = Color.gray, fontSize = 11, marginLeft = 2 } };
                globalFoldout.Add(emptyLabel);
            }

            scrollView.Add(globalFoldout);

            // Scene Events foldout (collapsed)
            var sceneFoldout = new Foldout
            {
                text = $"Scene Events ({sceneEventsByScene.Count} scenes)",
                value = false,
                style =
                {
                    marginLeft = 4,
                    marginBottom = 2
                }
            };

            var sortedSceneNames = sceneEventsByScene.Keys.OrderBy(n => n).ToList();
            foreach (var sceneName in sortedSceneNames)
            {
                var sceneList = sceneEventsByScene[sceneName];
                var sceneChildFoldout = new Foldout
                {
                    text = $"{sceneName} ({sceneList.Count})",
                    value = false,
                    style =
                    {
                        marginLeft = 10,
                        marginTop = 2
                    }
                };

                foreach (var eventLabel in sceneList
                             .AsValueEnumerable()
                             .Select(CreateEventLabel))
                {
                    sceneChildFoldout.Add(eventLabel);
                }

                sceneFoldout.Add(sceneChildFoldout);
            }

            if (sceneEventsByScene.Count == 0)
            {
                var emptyLabel = new Label("(none)") { style = { color = Color.gray, fontSize = 11, marginLeft = 2 } };
                sceneFoldout.Add(emptyLabel);
            }

            scrollView.Add(sceneFoldout);

            card.Add(scrollView);
            _eventsBrowserContainer.Add(card);
        }

        private static Label CreateEventLabel(EventScopeEntry entry)
        {
            var displayName = GetEventDisplayName(entry.typeFullName);
            var label = new Label($"• {displayName}")
            {
                style =
                {
                    fontSize = 12,
                    marginLeft = 16,
                    marginTop = 2,
                    marginBottom = 2
                }
            };
            return label;
        }

        private static string GetEventDisplayName(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return "(unknown)";

            try
            {
                // Try to get the short type name from the assembly-qualified name
                // Assembly-qualified name format: Namespace.TypeName, Assembly, Version=..., Culture=..., PublicKeyToken=...
                var typeMatch = Regex.Match(typeFullName, @"^([^,\s]+)\.([^,\s]+)");
                if (typeMatch.Success)
                {
                    var ns = typeMatch.Groups[1].Value;
                    var typeName = typeMatch.Groups[2].Value;

                    // If namespace is empty or just a common pattern, just show type name
                    if (string.IsNullOrEmpty(ns) || ns == "Global")
                        return typeName;

                    return $"{ns}.{typeName}";
                }

                // Fallback: just return the first part before comma
                var commaIndex = typeFullName.IndexOf(',');
                if (commaIndex > 0)
                    return typeFullName.Substring(0, commaIndex);

                return typeFullName;
            }
            catch
            {
                return typeFullName;
            }
        }

        private static EventScopeLookup LoadEventScopeLookup()
        {
            try
            {
                return Resources.Load<EventScopeLookup>(MessagePipeConstants.EventScopeLookupPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MessagePipe Settings] Failed to load EventScopeLookup: {ex.Message}");
                return null;
            }
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
