using System;
using System.Collections.Generic;
using System.Linq;
using _VuTH.Common.Editor.Settings;
using _VuTH.Common.Editor.Settings.Util;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Migrate;
using _VuTH.Core.Persistant.SaveSystem.Profile;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.Persistant.SaveSystem.Editor
{
    [SettingsTab]
    public class SaveServiceSettingsTab : ISettingsTab
    {
        public string Id => "SaveService";
        public string Title => "Save System";
        public int Order => 30; // After Bootstrap

        private SerializedObject _serializedProfile;
        private SaveServiceAdapterProfile _profile;

        // Cached items for dropdowns
        private List<AdapterTypeItem> _encryptorItems;
        private List<AdapterTypeItem> _serializerItems;
        private List<AdapterTypeItem> _backendItems;
        private List<AdapterTypeItem> _migratorItems;

        // Dropdown fields
        private DropdownField _encryptorDropdown;
        private DropdownField _serializerDropdown;
        private DropdownField _backendDropdown;
        private ListView _migratorListView;

        public VisualElement CreateView()
        {
            var container = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1
                }
            };

            // Try to load or create the profile
            if (!TryGetOrCreateProfile(out _profile))
            {
                container.Add(new Label("Error: Could not create or load SaveServiceAdapterProfile asset.")
                    { style = { color = Color.red } });
                return container;
            }

            _serializedProfile = new SerializedObject(_profile);

            // Initial scan BEFORE creating UI that uses the cached lists
            RefreshAvailableTypes();

            container.Add(new SettingTitle("Save System Settings"));
            container.Add(CreateSpacer(6));

            var statusSection = new SettingSection("Overview");
            statusSection.Add(new Label("Select adapters used for serialization, storage, and encryption.")
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.6f, 0.6f, 0.6f)
                }
            });
            container.Add(statusSection);
            container.Add(CreateSpacer(6));

            var profileSection = new SettingSection("Profile & Actions");
            profileSection.Add(CreateProfileActions());
            container.Add(profileSection);
            container.Add(CreateSpacer(6));

            var adapterSection = new SettingSection("Adapters");
            adapterSection.Add(CreateAdaptersSection());
            container.Add(adapterSection);
            container.Add(CreateSpacer(6));

            var migratorSection = new SettingSection("Migrators");
            migratorSection.Add(CreateMigratorsSection());
            container.Add(migratorSection);
            container.Add(CreateSpacer(4));

            return container;
        }

        private bool TryGetOrCreateProfile(out SaveServiceAdapterProfile profile)
        {
            profile = Resources.Load<SaveServiceAdapterProfile>("SaveServiceAdapterProfile");
            if (profile != null)
                return true;

            var guids = AssetDatabase.FindAssets("t:SaveServiceAdapterProfile");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                profile = AssetDatabase.LoadAssetAtPath<SaveServiceAdapterProfile>(path);
                if (profile != null)
                    return true;
            }

            // Create it
            profile = ScriptableObject.CreateInstance<SaveServiceAdapterProfile>();
            profile.ResetToDefaults();
            if (!AssetDatabase.IsValidFolder("Assets/_VuTH/Core/Persistant/SaveSystem/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/_VuTH/Core/Persistant/SaveSystem", "Resources");
            }

            AssetDatabase.CreateAsset(profile,
                "Assets/_VuTH/Core/Persistant/SaveSystem/Resources/SaveServiceAdapterProfile.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[SaveServiceSettingsTab] Created SaveServiceAdapterProfile.");
            return true;
        }

        private VisualElement CreateProfileActions()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 4,
                    marginBottom = 4
                }
            };

            var objectField = new ObjectField("Profile Asset")
            {
                objectType = typeof(SaveServiceAdapterProfile),
                value = _profile,
                style = { flexGrow = 1 }
            };
            objectField.SetEnabled(false);

            var pingButton = new Button(() => { EditorGUIUtility.PingObject(_profile); })
            {
                text = "Ping",
                style = { width = 64 }
            };

            var rescanButton = new Button(RefreshAvailableTypes)
            {
                text = "Rescan",
                style = { width = 80 }
            };

            var resetButton = new Button(() =>
            {
                if (EditorUtility.DisplayDialog("Reset to Defaults", "Reset all adapter selections to defaults?",
                        "Reset", "Cancel"))
                {
                    _profile.ResetToDefaults();
                    _serializedProfile.Update();
                    RefreshAvailableTypes();
                    UpdateAllDropdowns();
                }
            })
            {
                text = "Reset",
                style = { width = 80 }
            };

            row.Add(objectField);
            row.Add(CreateHSpacer(6));
            row.Add(pingButton);
            row.Add(CreateHSpacer(6));
            row.Add(rescanButton);
            row.Add(CreateHSpacer(6));
            row.Add(resetButton);
            return row;
        }

        private VisualElement CreateAdaptersSection()
        {
            var section = new VisualElement { style = { marginTop = 4 } };

            // Encryptor dropdown
            var encryptorRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };
            encryptorRow.Add(new Label("Encryptor")
            {
                style =
                {
                    width = 130,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            });
            encryptorRow.Add(CreateHSpacer(6));

            _encryptorDropdown = new DropdownField
            {
                style = { flexGrow = 1 },
                choices = _encryptorItems.Select(i => i.DisplayName).ToList()
            };
            _encryptorDropdown.RegisterValueChangedCallback(OnEncryptorChanged);
            encryptorRow.Add(_encryptorDropdown);
            section.Add(encryptorRow);

            // Serializer dropdown
            var serializerRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };
            serializerRow.Add(new Label("Serializer")
            {
                style =
                {
                    width = 130,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            });
            serializerRow.Add(CreateHSpacer(6));

            _serializerDropdown = new DropdownField
            {
                style = { flexGrow = 1 },
                choices = _serializerItems.Select(i => i.DisplayName).ToList()
            };
            _serializerDropdown.RegisterValueChangedCallback(OnSerializerChanged);
            serializerRow.Add(_serializerDropdown);
            section.Add(serializerRow);

            // Backend dropdown
            var backendRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };
            backendRow.Add(new Label("Backend")
            {
                style =
                {
                    width = 130,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            });
            backendRow.Add(CreateHSpacer(6));

            _backendDropdown = new DropdownField
            {
                style = { flexGrow = 1 },
                choices = _backendItems.Select(i => i.DisplayName).ToList()
            };
            _backendDropdown.RegisterValueChangedCallback(OnBackendChanged);
            backendRow.Add(_backendDropdown);
            section.Add(backendRow);

            // Set initial values
            UpdateAllDropdowns();

            return section;
        }

        private VisualElement CreateMigratorsSection()
        {
            var container = new VisualElement { style = { marginTop = 4 } };

            container.Add(new Label("Configure migration chain (ordered list).")
                { style = { fontSize = 12, color = new Color(0.6f, 0.6f, 0.6f), marginBottom = 2 } });
            container.Add(CreateSpacer(4));

            if (_migratorItems == null || _migratorItems.Count == 0)
            {
                container.Add(new HelpBox("No ISaveMigrator implementations found.", HelpBoxMessageType.Info));
                return container;
            }

            _migratorListView = new ListView
            {
                itemsSource = _migratorItems,
                makeItem = () => new Label { style = { paddingLeft = 6, paddingRight = 6, paddingTop = 2, paddingBottom = 2 } },
                bindItem = (element, i) => { ((Label)element).text = _migratorItems[i].DisplayName; },
                style =
                {
                    height = 120,
                    borderBottomWidth = 1,
                    borderTopWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderBottomColor = new Color(0, 0, 0, 0.25f),
                    borderTopColor = new Color(0, 0, 0, 0.25f),
                    borderLeftColor = new Color(0, 0, 0, 0.25f),
                    borderRightColor = new Color(0, 0, 0, 0.25f)
                }
            };

            container.Add(_migratorListView);
            return container;
        }

        private static VisualElement CreateSpacer(float height)
        {
            return new VisualElement { style = { height = height } };
        }

        private static VisualElement CreateHSpacer(float width)
        {
            return new VisualElement { style = { width = width } };
        }

        private void RefreshAvailableTypes()
        {
            _encryptorItems = SaveAdapterTypeScanner.GetAdapterItems(typeof(IEncryptor));
            _serializerItems = SaveAdapterTypeScanner.GetAdapterItems(typeof(ISerializer));
            _backendItems = SaveAdapterTypeScanner.GetAdapterItems(typeof(ISaveBackend));
            _migratorItems = SaveAdapterTypeScanner.GetAdapterItems(typeof(ISaveMigrator));

            Debug.Log($"[SaveServiceSettingsTab] Scanned types.");
        }

        private void UpdateAllDropdowns()
        {
            // Update Encryptor dropdown
            if (_profile.Encryptor != null)
            {
                var encryptorType = _profile.Encryptor.GetType();
                var encryptorItem = _encryptorItems.FirstOrDefault(i => i.Type == encryptorType);
                if (encryptorItem != null)
                {
                    _encryptorDropdown.SetValueWithoutNotify(encryptorItem.DisplayName);
                }
            }

            // Update Serializer dropdown
            if (_profile.Serializer != null)
            {
                var serializerType = _profile.Serializer.GetType();
                var serializerItem = _serializerItems.FirstOrDefault(i => i.Type == serializerType);
                if (serializerItem != null)
                {
                    _serializerDropdown.SetValueWithoutNotify(serializerItem.DisplayName);
                }
            }

            // Update Backend dropdown
            if (_profile.Backend != null)
            {
                var backendType = _profile.Backend.GetType();
                var backendItem = _backendItems.FirstOrDefault(i => i.Type == backendType);
                if (backendItem != null)
                {
                    _backendDropdown.SetValueWithoutNotify(backendItem.DisplayName);
                }
            }
        }

        private void OnEncryptorChanged(ChangeEvent<string> evt)
        {
            var selectedItem = _encryptorItems.FirstOrDefault(i => i.DisplayName == evt.newValue);
            if (selectedItem != null)
            {
                var instance = (IEncryptor)Activator.CreateInstance(selectedItem.Type);
                _profile.SetEncryptor(instance);
                _serializedProfile.Update();
                _serializedProfile.ApplyModifiedProperties();
            }
        }

        private void OnSerializerChanged(ChangeEvent<string> evt)
        {
            var selectedItem = _serializerItems.FirstOrDefault(i => i.DisplayName == evt.newValue);
            if (selectedItem != null)
            {
                var instance = (ISerializer)Activator.CreateInstance(selectedItem.Type);
                _profile.SetSerializer(instance);
                _serializedProfile.Update();
                _serializedProfile.ApplyModifiedProperties();
            }
        }

        private void OnBackendChanged(ChangeEvent<string> evt)
        {
            var selectedItem = _backendItems.FirstOrDefault(i => i.DisplayName == evt.newValue);
            if (selectedItem != null)
            {
                var instance = (ISaveBackend)Activator.CreateInstance(selectedItem.Type);
                _profile.SetBackend(instance);
                _serializedProfile.Update();
                _serializedProfile.ApplyModifiedProperties();
            }
        }
    }
}
