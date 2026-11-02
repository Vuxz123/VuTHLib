using System;
using System.Collections.Generic;
using _VuTH.Common.MessagePipe.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// UI Toolkit view for the "Assembly Whitelist" section of <see cref="MessagePipeSettingTab"/>.
    /// Lists, adds and removes entries of the <see cref="MessagePipeAssemblyWhitelist"/> asset.
    /// </summary>
    internal sealed class AssemblyWhitelistSection
    {
        private TextField _newAssemblyTextField;

        /// <summary>Container that receives the rendered content on each <see cref="Refresh"/>.</summary>
        public VisualElement Root { get; } = new() { style = { marginTop = 8 } };

        /// <summary>Reloads the whitelist asset and rebuilds the section content.</summary>
        public void Refresh()
        {
            Root.Clear();

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
                Root.Add(noteLabel);

                var createButton = new Button(CreateWhitelistAsset)
                {
                    text = "Create Whitelist Asset",
                    style = { width = 160 }
                };
                Root.Add(createButton);
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
            Root.Add(card);

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

            Root.Add(addRow);

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
            Root.Add(infoLabel);
        }

        private static MessagePipeAssemblyWhitelist LoadWhitelist()
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
                Refresh();
            }
            else
            {
                Debug.LogError("[MessagePipe Settings] Failed to create whitelist asset.");
            }
        }

        private void AddNewAssembly()
        {
            if (_newAssemblyTextField == null) return;

            var assemblyName = AssemblyWhitelistEditing.NormalizeName(_newAssemblyTextField.value);
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
            var assemblyNamesField = AssemblyWhitelistEditing.GetAssemblyNamesField();

            if (assemblyNamesField != null)
            {
                var assemblyNames = (List<string>)assemblyNamesField.GetValue(whitelist);
                if (assemblyNames == null)
                {
                    assemblyNames = new List<string>();
                    assemblyNamesField.SetValue(whitelist, assemblyNames);
                }

                // Check for duplicates (case-insensitive)
                var exists = AssemblyWhitelistEditing.ContainsName(assemblyNames, assemblyName);
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
                Refresh();

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

            var assemblyNamesField = AssemblyWhitelistEditing.GetAssemblyNamesField();

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

                Refresh();

                Debug.Log($"[MessagePipe Settings] Removed assembly '{removedName}' from whitelist.");
            }
            else
            {
                Debug.LogError("[MessagePipe Settings] Could not access assemblyNames field via reflection.");
            }
        }
    }
}
