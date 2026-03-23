using System;
using System.Collections.Generic;
using System.Linq;
using _VuTH.Common.Editor.Settings;
using _VuTH.Common.Editor.Settings.Util;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.Persistant.DataPackage.Editor
{
    [SettingsTab]
    public class DataPackageSettingTab : ISettingsTab
    {
        public string Id => "DataPackage";
        public string Title => "Data Package";
        public int Order => 31;

        private DataPackageProfile _profile;
        private List<PackageTypeItem> _availableTypes = new();
        private DropdownField _packageDropdown;
        private VisualElement _selectedPackagesContainer;

        public VisualElement CreateView()
        {
            var container = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1
                }
            };

            if (!DataPackageProfileUtilities.TryGetProfile(out _profile) || _profile == null)
            {
                container.Add(new Label("Error: Could not create or load DataPackageProfile asset.")
                {
                    style = { color = Color.red }
                });
                return container;
            }

            RefreshAvailableTypes();

            container.Add(new SettingTitle("Data Package Settings"));
            container.Add(CreateProfileSection());
            container.Add(CreateAddSection());
            container.Add(CreateSelectedPackagesSection());
            container.Add(CreateInfoSection());

            RefreshSelectedPackagesUI();
            return container;
        }

        private VisualElement CreateProfileSection()
        {
            var section = new SettingSection("Profile & Actions");

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var objectField = new ObjectField("Profile Asset")
            {
                objectType = typeof(DataPackageProfile),
                value = _profile,
                style = { flexGrow = 1 }
            };
            objectField.SetEnabled(false);

            var pingButton = new Button(() => EditorGUIUtility.PingObject(_profile))
            {
                text = "Ping",
                style = { width = 64 }
            };

            var rescanButton = new Button(() =>
            {
                RefreshAvailableTypes();
                RefreshDropdownChoices();
                RefreshSelectedPackagesUI();
            })
            {
                text = "Rescan",
                style = { width = 80 }
            };

            row.Add(objectField);
            row.Add(CreateHSpacer(6));
            row.Add(pingButton);
            row.Add(CreateHSpacer(6));
            row.Add(rescanButton);

            section.Add(row);
            return section;
        }

        private VisualElement CreateAddSection()
        {
            var section = new SettingSection("Add Package");

            _packageDropdown = new DropdownField("Available Packages")
            {
                style = { flexGrow = 1 }
            };
            RefreshDropdownChoices();

            var addButton = new Button(AddSelectedPackage)
            {
                text = "Add Package",
                style = { width = 120 }
            };

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexEnd
                }
            };

            row.Add(_packageDropdown);
            row.Add(CreateHSpacer(6));
            row.Add(addButton);

            section.Add(row);
            return section;
        }

        private VisualElement CreateSelectedPackagesSection()
        {
            var section = new SettingSection("Registered Packages");
            _selectedPackagesContainer = new VisualElement();
            section.Add(_selectedPackagesContainer);
            return section;
        }

        private static VisualElement CreateInfoSection()
        {
            var section = new SettingSection("Info");
            section.Add(new Label("• Packages listed here are instantiated from the profile asset."));
            section.Add(new Label("• DataPersistenceManager loads and registers them during bootstrap."));
            section.Add(new Label("• Only non-abstract IPersistencePackage types with a public parameterless constructor are shown."));
            return section;
        }

        private void RefreshAvailableTypes()
        {
            _availableTypes = TypeCache.GetTypesDerivedFrom<IPersistencePackage>()
                .Where(IsSelectablePackageType)
                .Select(type => new PackageTypeItem(type))
                .OrderBy(item => item.DisplayName)
                .ToList();
        }

        private void RefreshDropdownChoices()
        {
            if (_packageDropdown == null) return;

            var choices = _availableTypes.Select(item => item.DisplayLabel).ToList();
            if (choices.Count == 0)
            {
                choices.Add("(No packages found)");
            }

            _packageDropdown.choices = choices;
            _packageDropdown.SetValueWithoutNotify(choices[0]);
            _packageDropdown.SetEnabled(_availableTypes.Count > 0);
        }

        private void RefreshSelectedPackagesUI()
        {
            if (_selectedPackagesContainer == null) return;

            _selectedPackagesContainer.Clear();

            var selectedTypeNames = _profile.PackageTypeNames;
            if (selectedTypeNames == null || selectedTypeNames.Count == 0)
            {
                _selectedPackagesContainer.Add(new HelpBox(
                    "No persistence packages configured. Use the dropdown above to add one.",
                    HelpBoxMessageType.Info));
                return;
            }

            foreach (var typeName in selectedTypeNames)
            {
                var resolvedType = ResolveSelectedType(typeName);
                var displayName = resolvedType?.FullName ?? typeName;
                var isMissing = resolvedType == null;

                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 4
                    }
                };

                var label = new Label(displayName)
                {
                    style =
                    {
                        flexGrow = 1,
                        color = isMissing ? new Color(1f, 0.55f, 0.3f) : Color.white
                    }
                };

                var removeButton = new Button(() => RemovePackage(typeName))
                {
                    text = "Remove",
                    style = { width = 80 }
                };

                row.Add(label);

                if (!isMissing && IsBuiltInPackageType(resolvedType))
                {
                    row.Add(CreateHSpacer(6));
                    row.Add(CreateBuiltInBadge());
                }

                row.Add(CreateHSpacer(6));
                row.Add(removeButton);

                if (isMissing)
                {
                    row.Add(CreateHSpacer(6));
                    row.Add(new Label("(Type not found)")
                    {
                        style =
                        {
                            color = new Color(1f, 0.65f, 0.35f),
                            fontSize = 11
                        }
                    });
                }

                _selectedPackagesContainer.Add(row);
            }
        }

        private void AddSelectedPackage()
        {
            if (_availableTypes.Count == 0 || _packageDropdown == null) return;

            var selected = _availableTypes.FirstOrDefault(item => item.DisplayLabel == _packageDropdown.value);
            if (selected == null) return;

            var updated = _profile.PackageTypeNames.ToList();
            if (updated.Contains(selected.TypeName)) return;

            updated.Add(selected.TypeName);
            SaveProfile(updated);
        }

        private void RemovePackage(string typeName)
        {
            var updated = _profile.PackageTypeNames.Where(name => name != typeName).ToList();
            SaveProfile(updated);
        }

        private void SaveProfile(List<string> typeNames)
        {
            Undo.RecordObject(_profile, "Update Data Package Profile");
            _profile.SetPackageTypeNames(typeNames);
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
            RefreshSelectedPackagesUI();
        }

        private static bool IsSelectablePackageType(Type type)
        {
            return type.IsClass &&
                   !type.IsAbstract &&
                   !type.ContainsGenericParameters &&
                   type.GetConstructor(Type.EmptyTypes) != null;
        }

        private static bool IsBuiltInPackageType(Type type)
        {
            return type.Assembly.GetName().Name == "VuTH.Persistant.DataPackage" &&
                   type.Namespace != null &&
                   type.Namespace.StartsWith("_VuTH.Core.Persistant.DataPackage", StringComparison.Ordinal);
        }

        private static Type ResolveSelectedType(string typeName)
        {
            return Type.GetType(typeName, throwOnError: false) ??
                   AppDomain.CurrentDomain.GetAssemblies()
                       .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                       .FirstOrDefault(type => type != null);
        }

        private static VisualElement CreateHSpacer(float width)
        {
            return new VisualElement { style = { width = width } };
        }

        private static VisualElement CreateBuiltInBadge()
        {
            return new Label("Built-in")
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
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
        }

        private sealed class PackageTypeItem
        {
            public PackageTypeItem(Type type)
            {
                TypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
                DisplayName = type.FullName ?? type.Name;
                DisplayLabel = IsBuiltInPackageType(type)
                    ? $"{DisplayName} [Built-in]"
                    : DisplayName;
            }

            public string TypeName { get; }
            public string DisplayName { get; }
            public string DisplayLabel { get; }
        }
    }
}
