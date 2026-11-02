using System;
using _VuTH.Common.MessagePipe.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// UI Toolkit view for the "MessagePipe Options" section of <see cref="MessagePipeSettingTab"/>.
    /// Loads, creates and edits the <see cref="MessagePipeOptionsConfig"/> asset.
    /// </summary>
    internal sealed class MessagePipeOptionsSection
    {
        private Toggle _enableCaptureStackTraceToggle;
        private Toggle _preserveRegistrarToggle;
        private MessagePipeOptionsConfig _optionsConfig;

        /// <summary>Container that receives the rendered content on each <see cref="Refresh"/>.</summary>
        public VisualElement Root { get; } = new() { style = { marginTop = 8 } };

        /// <summary>Reloads the options config asset and rebuilds the section content.</summary>
        public void Refresh()
        {
            Root.Clear();

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

                Root.Add(missingCard);
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

            Root.Add(card);
        }

        private static MessagePipeOptionsConfig LoadOptionsConfig()
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
                Refresh();
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
    }
}
