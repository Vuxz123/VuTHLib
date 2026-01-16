using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Common.Editor.Settings
{
    public class SettingsWindow : EditorWindow
    {
        private ListView _sidebar;
        private VisualElement _content;

        [MenuItem("VuTH/Settings")]
        public static void Open()
        {
            var wnd = GetWindow<SettingsWindow>();
            wnd.titleContent = new GUIContent("Screen Flow");
            wnd.minSize = new Vector2(600, 400);
            wnd.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Row;

            _sidebar = new ListView
            {
                itemsSource = (IList)SettingsRegistry.Tabs,
                makeItem = MakeItem,
                bindItem = BindItem,
                selectionType = SelectionType.Single,
                style =
                {
                    width = 180,
                    borderRightWidth = 1,
                    borderRightColor = new Color(0.15f, 0.15f, 0.15f) // Màu viền tối
                }
            };

            _sidebar.makeItem = MakeItem;

            _sidebar.selectionChanged += OnTabSelected;

            _content = new VisualElement
            {
                style = { flexGrow = 1 }
            };

            rootVisualElement.Add(_sidebar);
            rootVisualElement.Add(_content);

            _sidebar.SetSelection(0);
        }

        private void OnTabSelected(IEnumerable<object> selection)
        {
            _content.Clear();

            if (selection.FirstOrDefault() is ISettingsTab tab)
            {
                _content.Add(tab.CreateView());
            }
            
            _sidebar.RefreshItems();
        }

        private static VisualElement MakeItem()
        {
            var label = new Label
            {
                style =
                {
                    // Thiết lập style để hiển thị đẹp hơn trong Sidebar
                    height = 30, // Chiều cao tiêu chuẩn cho mỗi dòng menu
                    unityTextAlign = TextAnchor.MiddleLeft, // Căn chữ nằm giữa theo chiều dọc
                    paddingLeft = 10, // Khoảng cách lề trái
                    paddingRight = 5
                }
            };

            // Tùy chọn: Thêm font style nếu muốn
            // label.style.unityFontStyleAndWeight = FontStyle.Bold;

            return label;
        }

        private void BindItem(VisualElement item, int i)
        {
            var label = (Label)item;
            label.text = SettingsRegistry.Tabs[i].Title;
            
            // Kiểm tra xem dòng này có đang được chọn không
            if (_sidebar.selectedIndex == i)
            {
                // Màu xanh highlight (giống Unity Project window)
                label.style.backgroundColor = new Color(0.17f, 0.36f, 0.53f); 
                label.style.color = Color.white; // Chữ trắng cho nổi
            }
            else
            {
                // Trạng thái bình thường
                label.style.backgroundColor = Color.clear;
                label.style.color = new Color(0.8f, 0.8f, 0.8f); // Màu chữ xám nhạt mặc định
            }
        }
    }
}