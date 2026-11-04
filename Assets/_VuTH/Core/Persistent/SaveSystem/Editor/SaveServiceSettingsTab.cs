using System;
using System.Collections.Generic;
using System.Linq;
using _VuTH.Common.Editor.Settings;
using _VuTH.Common.Editor.Settings.Util;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using _VuTH.Core.Persistent.SaveSystem.Migrate;
using _VuTH.Core.Persistent.SaveSystem.Profile;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.Persistent.SaveSystem.Editor
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

            var migratorSection = new SettingSection("Versioning & Migrators");
            migratorSection.Add(CreateMigratorsSection());
            container.Add(migratorSection);
            container.Add(CreateSpacer(4));

            return container;
        }

        private bool TryGetOrCreateProfile(out SaveServiceAdapterProfile profile)
        {
            profile = Resources.Load<SaveServiceAdapterProfile>(SaveServiceAdapterProfile.ResourcePath);
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
            if (!AssetDatabase.IsValidFolder("Assets/_VuTH/Core/Persistent/SaveSystem/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/_VuTH/Core/Persistent/SaveSystem", "Resources");
            }

            AssetDatabase.CreateAsset(profile,
                "Assets/_VuTH/Core/Persistent/SaveSystem/Resources/SaveServiceAdapterProfile.asset");
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
                    Undo.RecordObject(_profile, "Reset Save Profile");
                    _profile.ResetToDefaults();
                    EditorUtility.SetDirty(_profile);
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

            container.Add(new Label(
                    "Bump the schema version when the saved data format changes, and add one migrator per version " +
                    "step (a DefaultSaveMigrator when the format did not change). Saves from a version without a " +
                    "migrator are reported as MigrationFailed and left untouched.")
                {
                    style =
                    {
                        fontSize = 11, color = new Color(0.6f, 0.6f, 0.6f), marginBottom = 4,
                        whiteSpace = WhiteSpace.Normal
                    }
                });

            var versionField = new PropertyField(_serializedProfile.FindProperty("schemaVersion"), "Schema Version");
            versionField.Bind(_serializedProfile);
            container.Add(versionField);

            var migratorsField = new PropertyField(_serializedProfile.FindProperty("migrators"), "Migrators");
            migratorsField.Bind(_serializedProfile);
            container.Add(migratorsField);
            container.Add(CreateSpacer(4));

            if (_migratorItems == null || _migratorItems.Count == 0)
            {
                container.Add(new HelpBox(
                    "No ISaveMigrator implementation with [Serializable] and a public parameterless constructor found.",
                    HelpBoxMessageType.Info));
                return container;
            }

            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var typeDropdown = new DropdownField
            {
                style = { flexGrow = 1 },
                choices = _migratorItems.Select(i => i.DisplayName).ToList(),
                index = 0
            };
            var addButton = new Button(() => AddMigrator(typeDropdown.index)) { text = "Add Migrator", style = { width = 110 } };
            addRow.Add(typeDropdown);
            addRow.Add(CreateHSpacer(6));
            addRow.Add(addButton);
            container.Add(addRow);

            return container;
        }

        private void AddMigrator(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _migratorItems.Count) return;

            var instance = (ISaveMigrator)Activator.CreateInstance(_migratorItems[itemIndex].Type);
            Undo.RecordObject(_profile, "Add Save Migrator");
            _profile.SetMigrators(new List<ISaveMigrator>(_profile.Migrators) { instance });
            EditorUtility.SetDirty(_profile);
            _serializedProfile.Update();
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
                Undo.RecordObject(_profile, "Change Save Encryptor");
                _profile.SetEncryptor(instance);
                EditorUtility.SetDirty(_profile);
                _serializedProfile.Update();
            }
        }

        private void OnSerializerChanged(ChangeEvent<string> evt)
        {
            var selectedItem = _serializerItems.FirstOrDefault(i => i.DisplayName == evt.newValue);
            if (selectedItem != null)
            {
                var instance = (ISerializer)Activator.CreateInstance(selectedItem.Type);
                Undo.RecordObject(_profile, "Change Save Serializer");
                _profile.SetSerializer(instance);
                EditorUtility.SetDirty(_profile);
                _serializedProfile.Update();
            }
        }

        private void OnBackendChanged(ChangeEvent<string> evt)
        {
            var selectedItem = _backendItems.FirstOrDefault(i => i.DisplayName == evt.newValue);
            if (selectedItem != null)
            {
                var instance = (ISaveBackend)Activator.CreateInstance(selectedItem.Type);
                Undo.RecordObject(_profile, "Change Save Backend");
                _profile.SetBackend(instance);
                EditorUtility.SetDirty(_profile);
                _serializedProfile.Update();
            }
        }
    }
}
