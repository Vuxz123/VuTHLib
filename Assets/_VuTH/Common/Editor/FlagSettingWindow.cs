using System;
using System.Collections.Generic;
using System.IO;
using Common.Editor.Settings;
using Common.Editor.Settings.Util;
using Common.Log;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace Common.Editor
{
    [SettingsTab]
    public class FlagSettingWindow : ISettingsTab
    {
        private readonly string[] _features = new []
        {
            "VCONTAINER"
        };
        
        private const string FlagDataPath =
            "Assets/_VuTH/Common/Editor/FlagData/feature_flags.json";

        public string Id => "FlagSetting";
        public string Title => "Feature Flag";
        public int Order => 99;

        private readonly Dictionary<string, bool> _featureFlags = new();

        public VisualElement CreateView()
        {
            BuildDictionary();

            var root = new VisualElement();
            
            root.Add(new SettingTitle("Feature Flags"));

            foreach (var kv in _featureFlags)
            {
                var toggle = new Toggle(kv.Key)
                {
                    value = kv.Value
                };

                toggle.RegisterValueChangedCallback(evt =>
                {
                    _featureFlags[kv.Key] = evt.newValue;
                    SaveData();
                    UpdateFeatureFlag(kv.Key, evt.newValue);
                });

                root.Add(toggle);
            }

            return root;
        }
        
        // =============================
        // Logic
        // =============================
        private void UpdateFeatureFlag(string featureName, bool state)
        {
            // 1. Xác định target build hiện tại
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

            // 2. Lấy danh sách symbols hiện tại (dạng string, cách nhau bởi dấu ;)
            var currentSymbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
    
            // 3. Chuyển thành List để dễ thao tác
            var symbolList = currentSymbols.Split(';').AsValueEnumerable()
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            // 4. Thực hiện logic thêm hoặc xóa
            if (state)
            {
                if (!symbolList.Contains(featureName))
                {
                    symbolList.Add(featureName);
                }
            }
            else
            {
                if (symbolList.Contains(featureName))
                {
                    symbolList.Remove(featureName);
                }
            }

            // 5. Gộp lại thành string và lưu lại vào PlayerSettings
            var newSymbols = string.Join(";", symbolList.ToArray());
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newSymbols);
    
            // Thông báo để theo dõi
            this.Log($"{featureName} has been {(state ? "Added" : "Removed")}.");
        }

        // =============================
        // Data
        // =============================

        private void BuildDictionary()
        {
            _featureFlags.Clear();

            var data = LoadData();
            if (data.featureName == null || data.featureState == null)
                return;

            var count = Mathf.Min(
                data.featureName.Length,
                data.featureState.Length
            );

            for (var i = 0; i < count; i++)
            {
                _featureFlags[data.featureName[i]] = data.featureState[i];
            }
        }

        private FeatureFlags LoadData()
        {
            if (!File.Exists(FlagDataPath))
            {
                var fn = new string[_features.Length];
                var fs = new bool[_features.Length];
                Array.Copy(_features, fn, _features.Length);
                
                var @default = new FeatureFlags
                {
                    featureName = fn,
                    featureState = fs
                };
                
                return @default;
            }

            var json = File.ReadAllText(FlagDataPath);
            return JsonUtility.FromJson<FeatureFlags>(json);
        }

        private void SaveData()
        {
            var data = new FeatureFlags
            {
                featureName = new string[_featureFlags.Count],
                featureState = new bool[_featureFlags.Count]
            };

            var index = 0;
            foreach (var kv in _featureFlags)
            {
                data.featureName[index] = kv.Key;
                data.featureState[index] = kv.Value;
                index++;
            }

            var json = JsonUtility.ToJson(data, true);
            if (!Directory.Exists(Path.GetDirectoryName(FlagDataPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FlagDataPath) ?? throw new InvalidOperationException());
            }
            File.WriteAllText(FlagDataPath, json);

            AssetDatabase.Refresh();
        }

        // =============================
        // Model
        // =============================

        [Serializable]
        private struct FeatureFlags
        {
            public string[] featureName;
            public bool[] featureState;
        }
    }
}
