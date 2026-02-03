using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using _VuTH.Core.GameCycle.Screen.Core.A;
using _VuTH.Core.GameCycle.Screen.Identifier;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Editor.Identifier
{
    public class ScreenIdentifierEditorWindow : EditorWindow
    {
        [MenuItem("VuTH/Core/Screen/Screen Identifier Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<ScreenIdentifierEditorWindow>("Screen Identifier Editor");
            window.minSize = new Vector2(450, 400);
            window.Show();
        }
        
        // --- PATH CONFIGURATION ---
        // Đường dẫn lưu file ID assets
        private const string SavePath = "Assets/Game/Resources/GameCycle/Screens/Ids";
        
        // Đường dẫn bảo vệ (Default Resources)
        private const string ProtectedDefaultPath = "Assets/_VuTH/Core/DefaultResources/GameCycle/ScreenIdentifiers";
        
        // Đường dẫn để lưu file code C# được sinh ra
        // Lưu ý: Folder này phải tồn tại. Tool sẽ tự tạo nếu chưa có.
        private const string CodeGenerationPath = "Assets/Game/Scripts/Generated";
        private const string CodeGenerationFileName = "ScreenIds.cs";

        // ---------------------------

        private string _newIdName = "";
        private Vector2 _scrollPos;
        
        private readonly List<ScreenIdentifier> _allIds = new();
        private readonly Dictionary<ScreenIdentifier, List<ScreenModel>> _dependencyMap = new();

        private void OnEnable()
        {
            RefreshData();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawCreateSection();
            EditorGUILayout.Space();
            DrawListSection();
        }

        private void RefreshData()
        {
            _allIds.Clear();
            _dependencyMap.Clear();

            var idGuids = AssetDatabase.FindAssets("t:ScreenIdentifier");
            foreach (var guid in idGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var idAsset = AssetDatabase.LoadAssetAtPath<ScreenIdentifier>(path);
                if (!idAsset) continue;
                _allIds.Add(idAsset);
                _dependencyMap[idAsset] = new List<ScreenModel>();
            }

            var modelGuids = AssetDatabase.FindAssets("t:ScreenModel");
            foreach (var guid in modelGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<ScreenModel>(path);
                
                if (!model || !model.screenId) continue;
                if (_dependencyMap.TryGetValue(model.screenId, out var value))
                {
                    value.Add(model);
                }
            }
            
            _allIds.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Screen Identity Manager", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total IDs: {_allIds.Count} | Models Scanned: {AssetDatabase.FindAssets("t:ScreenModel").Length}");
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Data"))
            {
                RefreshData();
            }
            
            // Nút bấm thủ công để Regenerate Code nếu cần
            if (GUILayout.Button("Force Regenerate Code"))
            {
                GenerateAccessCode();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawCreateSection()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Create New Identifier", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            _newIdName = EditorGUILayout.TextField("ID Name", _newIdName);
            
            GUI.enabled = !string.IsNullOrEmpty(_newIdName);
            if (GUILayout.Button("Create", GUILayout.Width(80)))
            {
                CreateNewId(_newIdName);
                _newIdName = ""; 
                GUI.FocusControl(null); 
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox($"Asset Path: {SavePath}\nCode Path: {CodeGenerationPath}/{CodeGenerationFileName}", MessageType.Info);
        }

        private void DrawListSection()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Existing Identifiers", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var id in _allIds.Where(id => id))
            {
                DrawIdRow(id);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIdRow(ScreenIdentifier id)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            var usedBy = _dependencyMap[id];
            var isUsed = usedBy.Count > 0;
            
            var icon = EditorGUIUtility.IconContent(isUsed ? "d_Linked" : "d_Unlinked");
            GUILayout.Label(icon, GUILayout.Width(20));
            
            if (GUILayout.Button(id.name, EditorStyles.label, GUILayout.ExpandWidth(true)))
            {
                EditorGUIUtility.PingObject(id);
                Selection.activeObject = id;
            }

            string assetPath = AssetDatabase.GetAssetPath(id);
            bool isDefault = IsProtectedPath(assetPath);

            if (isDefault)
            {
                GUI.enabled = false;
                GUILayout.Button("Default", GUILayout.Width(60));
                GUI.enabled = true;
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    TryDeleteId(id, isUsed);
                }
                GUI.backgroundColor = Color.white;
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(25);
            if (isUsed)
            {
                EditorGUILayout.LabelField("Used in:", EditorStyles.miniLabel, GUILayout.Width(50));
                foreach (var model in usedBy.Where(model => GUILayout.Button(model.name, EditorStyles.miniButton, GUILayout.Width(100))))
                {
                    Selection.activeObject = model;
                }
                GUILayout.FlexibleSpace();
            }
            else
            {
                EditorGUILayout.LabelField("Status: Unused", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        // --- ACTIONS ---

        private void CreateNewId(string idName)
        {
            if (!Directory.Exists(SavePath)) Directory.CreateDirectory(SavePath);

            var fileName = idName.StartsWith("ID_") ? idName : "ID_" + idName;
            var fullPath = $"{SavePath}/{fileName}.asset";

            if (AssetDatabase.LoadAssetAtPath<ScreenIdentifier>(fullPath))
            {
                EditorUtility.DisplayDialog("Error", $"ID '{fileName}' already exists!", "OK");
                return;
            }

            var newAsset = CreateInstance<ScreenIdentifier>();
            AssetDatabase.CreateAsset(newAsset, fullPath);
            AssetDatabase.SaveAssets();
            
            RefreshData();
            
            // TỰ ĐỘNG SINH CODE SAU KHI TẠO
            GenerateAccessCode(); 
            
            Debug.Log($"Created ID & Regenerated Code: {fileName}");
        }

        private void TryDeleteId(ScreenIdentifier id, bool isUsed)
        {
            if (IsProtectedPath(AssetDatabase.GetAssetPath(id)))
            {
                EditorUtility.DisplayDialog("Restricted", "Cannot delete Default Core Identifier!", "OK");
                return;
            }

            if (isUsed)
            {
                var confirm = EditorUtility.DisplayDialog("Warning", 
                    $"'{id.name}' is used by {_dependencyMap[id].Count} Models.\nDelete anyway?", "Delete", "Cancel");
                if (!confirm) return;
            }
            else
            {
                if (!EditorUtility.DisplayDialog("Confirm", $"Delete '{id.name}'?", "Yes", "No")) return;
            }

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(id));
            RefreshData();
            
            // TỰ ĐỘNG SINH CODE SAU KHI XÓA
            GenerateAccessCode();
        }

        private bool IsProtectedPath(string path)
        {
            return path.Replace("\\", "/").StartsWith(ProtectedDefaultPath.Replace("\\", "/"));
        }

        // --- CODE GENERATION LOGIC ---

        #region Code Generation

        private void GenerateAccessCode()
        {
            // 1. Đảm bảo thư mục tồn tại
            if (!Directory.Exists(CodeGenerationPath))
            {
                Directory.CreateDirectory(CodeGenerationPath);
            }

            // 2. Chuẩn bị nội dung
            var sb = new StringBuilder();
            sb.AppendLine("// ------------------------------------------------------------------------------");
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("//     This code was generated by ScreenIdentifierEditorWindow.");
            sb.AppendLine("//     Runtime Changes to this file may cause incorrect behavior and will be lost if");
            sb.AppendLine("//     the code is regenerated.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("// ------------------------------------------------------------------------------");
            sb.AppendLine("");
            sb.AppendLine("using Core.GameCycle.Screen;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("");
            sb.AppendLine("namespace Core.Generated"); // Namespace bạn muốn
            sb.AppendLine("{");
            sb.AppendLine("    public static class ScreenIds");
            sb.AppendLine("    {");

            foreach (var id in _allIds)
            {
                if (!id) continue;

                // Tên biến (clean ký tự lạ, khoảng trắng)
                var varName = id.name.Replace("ID_", "").Replace(" ", "_");
                
                // Lấy đường dẫn Resource để load runtime
                // Lưu ý: Đường dẫn Resource phải tính từ sau folder "Resources/" và bỏ đuôi .asset
                var resourcePath = GetResourcePath(AssetDatabase.GetAssetPath(id));

                if (!string.IsNullOrEmpty(resourcePath))
                {
                    sb.AppendLine($"        private static ScreenIdentifier _{varName};");
                    sb.AppendLine($"        public static ScreenIdentifier {varName} => _{varName} ??= Resources.Load<ScreenIdentifier>(\"{resourcePath}\");");
                    sb.AppendLine("");
                }
                else
                {
                    // Fallback nếu asset không nằm trong Resources folder (Load bằng GUID - Editor only hoặc Addressables)
                    // Ở đây mình comment cảnh báo
                    sb.AppendLine($"        // Warning: '{id.name}' is not in a 'Resources' folder. Cannot generate runtime static accessor.");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            // 3. Ghi file
            var fullPath = Path.Combine(CodeGenerationPath, CodeGenerationFileName);
            File.WriteAllText(fullPath, sb.ToString());
            
            // 4. Refresh để Unity compile lại
            AssetDatabase.Refresh();
        }

        // Helper để lấy đường dẫn tương đối trong Resources
        private static string GetResourcePath(string assetPath)
        {
            // assetPath ví dụ: "Assets/Game/Resources/GameCycle/Screens/Ids/ID_MainMenu.asset"
            const string pattern = "/Resources/";
            var index = assetPath.IndexOf(pattern, StringComparison.Ordinal);
            
            if (index < 0) return null; // Không nằm trong Resources

            // Cắt lấy phần sau Resources/
            var relative = assetPath[(index + pattern.Length)..];
            
            // Bỏ đuôi .asset
            var extensionIndex = relative.LastIndexOf('.');
            if (extensionIndex >= 0)
            {
                relative = relative[..extensionIndex];
            }

            return relative;
        }

        #endregion
    }
}