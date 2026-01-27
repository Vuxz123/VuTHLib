using System;
using System.Linq;
using _VuTH.Core.GameCycle.Screen.GlobalEvent;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Editor.GlobalEvent
{
    [CustomEditor(typeof(GlobalScreenEventProfile))]
    public class GlobalScreenEventProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty _listenersProp;
        private GUIStyle _headerStyle;
        private GUIStyle _boxStyle;

        private void OnEnable()
        {
            _listenersProp = serializedObject.FindProperty("configuredListeners");
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.toolbar);
                _headerStyle.fontStyle = FontStyle.Bold;
                _headerStyle.alignment = TextAnchor.MiddleLeft;
                _headerStyle.padding = new RectOffset(5, 5, 0, 0);
            }

            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(EditorStyles.helpBox);
                _boxStyle.padding = new RectOffset(5, 5, 5, 5);
                _boxStyle.margin = new RectOffset(0, 0, 5, 5);
            }
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("📢 Global Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Danh sách các Listener sẽ được kích hoạt cùng hệ thống.", MessageType.None);
            EditorGUILayout.Space(5);

            DrawListenersList();

            EditorGUILayout.Space(10);
            
            // Nút Add to bự, màu xanh (nếu muốn) hoặc mặc định
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("+ Add New Listener", GUILayout.Height(30)))
            {
                ShowAddListenerMenu();
            }
            GUI.backgroundColor = Color.white;

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawListenersList()
        {
            if (_listenersProp.arraySize == 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Check Empty List (0 items)", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < _listenersProp.arraySize; i++)
            {
                SerializedProperty p = _listenersProp.GetArrayElementAtIndex(i);
                DrawListenerItem(p, i);
            }
        }

        private void DrawListenerItem(SerializedProperty prop, int index)
        {
            EditorGUILayout.BeginVertical(_boxStyle);

            // --- HEADER ROW ---
            EditorGUILayout.BeginHorizontal(_headerStyle);
            
            // 1. Foldout & Name
            string typeName = GetManagedTypeName(prop);
            prop.isExpanded = EditorGUILayout.Foldout(prop.isExpanded, typeName, true);

            GUILayout.FlexibleSpace();

            // 2. Button PING SCRIPT (Icon Script)
            GUIContent pingIcon = EditorGUIUtility.IconContent("cs Script Icon");
            pingIcon.tooltip = "Ping script file in Project";
            if (GUILayout.Button(pingIcon, EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                PingScriptFile(prop);
            }

            // 3. Button REMOVE (Icon X)
            GUIContent removeIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
            removeIcon.tooltip = "Remove this listener";
            
            // Đổi màu nút xóa sang đỏ nhạt để cảnh báo
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); 
            if (GUILayout.Button(removeIcon, EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                _listenersProp.DeleteArrayElementAtIndex(index);
                GUI.backgroundColor = oldColor;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return; // Thoát ngay sau khi xóa để tránh lỗi GUI layout
            }
            GUI.backgroundColor = oldColor;

            EditorGUILayout.EndHorizontal();
            // --- END HEADER ---

            // --- BODY (PROPERTIES) ---
            if (prop.isExpanded)
            {
                EditorGUILayout.Space(2);
                EditorGUI.indentLevel++;
                
                SerializedProperty endProp = prop.GetEndProperty();
                SerializedProperty child = prop.Copy();
                
                if (child.NextVisible(true)) 
                {
                    while (!SerializedProperty.EqualContents(child, endProp))
                    {
                        EditorGUILayout.PropertyField(child, true);
                        if (!child.NextVisible(false)) break;
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No serialized fields.", EditorStyles.miniLabel);
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
        }

        // Logic tìm và Ping file script
        private void PingScriptFile(SerializedProperty prop)
        {
            object obj = prop.managedReferenceValue;
            if (obj == null) return;

            Type type = obj.GetType();
            
            // Tìm asset script theo tên Type (Unity Convention: File name = Class name)
            string[] guids = AssetDatabase.FindAssets("t:MonoScript " + type.Name);
            
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null)
                {
                    EditorGUIUtility.PingObject(script);
                    return;
                }
            }
            
            Debug.LogWarning($"Could not find script file for type '{type.Name}'. Make sure file name matches class name.");
        }

        private void ShowAddListenerMenu()
        {
            GenericMenu menu = new GenericMenu();

            var types = TypeCache.GetTypesDerivedFrom<IScreenEventListener>()
                .Where(t => !t.IsAbstract && !t.IsInterface && t.IsSerializable)
                .OrderBy(t => t.Name)
                .ToList();

            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No [Serializable] IScreenEventListener found"));
                menu.ShowAsContext();
                return;
            }

            foreach (var type in types)
            {
                // Group theo Namespace để menu gọn hơn nếu có nhiều class
                var displayPath = type.FullName?.Replace('.', '/');
                // Hoặc chỉ hiển thị tên Class nếu muốn ngắn gọn: 
                // string displayPath = type.Name; 
                
                menu.AddItem(new GUIContent(displayPath), false, OnAddListener, type);
            }

            menu.ShowAsContext();
        }

        private void OnAddListener(object typeObj)
        {
            Type type = (Type)typeObj;
            object newInstance = Activator.CreateInstance(type);

            _listenersProp.arraySize++;
            var element = _listenersProp.GetArrayElementAtIndex(_listenersProp.arraySize - 1);
            element.managedReferenceValue = newInstance;

            serializedObject.ApplyModifiedProperties();
        }

        private string GetManagedTypeName(SerializedProperty prop)
        {
            // Lấy tên class từ Managed Reference (trông gọn hơn full type name)
            string fullType = prop.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(fullType)) return "Null";
            
            int lastDot = fullType.LastIndexOf('.');
            string className = (lastDot != -1) ? fullType.Substring(lastDot + 1) : fullType;
            
            // Tách PascalCase thành text dễ đọc (VD: AudioListener -> Audio Listener) - Optional
            return ObjectNames.NicifyVariableName(className);
        }
    }
}