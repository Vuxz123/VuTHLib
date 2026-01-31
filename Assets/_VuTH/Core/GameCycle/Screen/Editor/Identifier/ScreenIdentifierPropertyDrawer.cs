using System.Collections.Generic;
using System.Linq;
using _VuTH.Core.GameCycle.Screen.Identifier;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Editor.Identifier
{
    [CustomPropertyDrawer(typeof(ScreenIdentifier))] 
    public class ScreenIdentifierPropertyDrawer : PropertyDrawer
    {
        private static ScreenIdentifier[] _cachedIds;
        private static string[] _cachedNames;
        private const float ButtonWidth = 24f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // --- THAY ĐỔI QUAN TRỌNG NHẤT ---
            // Vì ScreenIdentifier là SO, nên chính property này là biến chứa reference.
            // Không tìm biến con "id" nữa.
            SerializedProperty idProp = property; 
            // --------------------------------

            // 1. Vẽ Label
            Rect contentRect = EditorGUI.PrefixLabel(position, label);

            // 2. Tính toán Rect
            Rect dropdownRect = new Rect(contentRect.x, contentRect.y, 
                contentRect.width - ButtonWidth - 2, contentRect.height);
            Rect buttonRect = new Rect(contentRect.x + contentRect.width - ButtonWidth, 
                contentRect.y, ButtonWidth, contentRect.height);

            // 3. Cache Data
            if (_cachedIds == null || Event.current.type == EventType.Layout)
            {
                ReloadIdCache();
            }

            // 4. Tìm index hiện tại
            int currentIndex = 0; // 0 = None
            if (idProp.objectReferenceValue != null)
            {
                // So sánh Reference trực tiếp
                for (int i = 0; i < _cachedIds.Length; i++)
                {
                    if (_cachedIds[i] == idProp.objectReferenceValue)
                    {
                        currentIndex = i + 1; // +1 do có option None
                        break;
                    }
                }
            }

            // 5. Vẽ Dropdown
            int newIndex = EditorGUI.Popup(dropdownRect, currentIndex, _cachedNames);

            // 6. Cập nhật giá trị nếu thay đổi
            if (newIndex != currentIndex)
            {
                if (newIndex == 0)
                {
                    idProp.objectReferenceValue = null;
                }
                else
                {
                    // Đảm bảo index an toàn
                    if (newIndex - 1 < _cachedIds.Length)
                    {
                        idProp.objectReferenceValue = _cachedIds[newIndex - 1];
                    }
                }
            }

            // 7. Nút mở Editor Window
            GUIContent btnIcon = EditorGUIUtility.IconContent("d_Settings");
            btnIcon.tooltip = "Open Screen Identifier Manager";

            if (GUI.Button(buttonRect, btnIcon, EditorStyles.iconButton))
            {
                ScreenIdentifierEditorWindow.OpenWindow();
            }

            EditorGUI.EndProperty();
        }

        private static void ReloadIdCache()
        {
            var guids = AssetDatabase.FindAssets("t:ScreenIdentifier");
            var idsList = new List<ScreenIdentifier>();
            var namesList = new List<string> { "- None -" };

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var idAsset = AssetDatabase.LoadAssetAtPath<ScreenIdentifier>(path);
                if (!idAsset) continue;
                idsList.Add(idAsset);
                namesList.Add(idAsset.name);
            }
            
            // Sort danh sách theo tên A-Z
            var sortedData = idsList.Zip(namesList.Skip(1), (id, name) => new { Id = id, Name = name })
                                    .OrderBy(x => x.Name)
                                    .ToList();

            _cachedIds = sortedData.Select(x => x.Id).ToArray();
            
            var finalNames = new List<string> { "- None -" };
            finalNames.AddRange(sortedData.Select(x => x.Name));
            _cachedNames = finalNames.ToArray();
        }
    }
}