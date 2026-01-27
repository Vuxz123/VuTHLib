using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
// Cần thiết cho UI Toolkit

namespace _VuTH.Core.Window.Transition.Editor
{
    [CustomPropertyDrawer(typeof(TransitionSettingsReferenceAttribute))]
    public sealed class TransitionSettingsReferenceDrawer : PropertyDrawer
    {
        // Struct lưu thông tin Type
        private struct Option
        {
            public Type Type;
            public string Name;
        }

        // Cache tĩnh để không phải reflection lại liên tục
        private static Option[] _options;
        private static List<string> _displayNames;

        // ============================================================
        // LOGIC REFLECTION (Giữ nguyên logic lõi, thay ZLinq bằng Linq chuẩn)
        // ============================================================
        private static void EnsureCache()
        {
            if (_options != null && _displayNames != null) return;

            var baseType = typeof(UITransitionSettings);
            var types = new List<Type>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] asmTypes;
                try
                {
                    asmTypes = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    asmTypes = e.Types.Where(t => t != null).ToArray();
                }

                foreach (var t in asmTypes)
                {
                    if (t == null || t.IsAbstract || t.IsGenericTypeDefinition) continue;
                    if (baseType.IsAssignableFrom(t))
                    {
                        types.Add(t);
                    }
                }
            }

            var optionsList = new List<Option>
            {
                new Option { Type = null, Name = "None" }
            };

            // Dùng LINQ chuẩn
            optionsList.AddRange(types
                .Select(t => new { t, attr = t.GetCustomAttribute<TransitionSettingsNameAttribute>() })
                .Select(x => new Option
                {
                    Type = x.t,
                    Name = string.IsNullOrWhiteSpace(x.attr?.Name) ? ObjectNames.NicifyVariableName(x.t.Name) : x.attr.Name
                })
                .OrderBy(o => o.Name));

            _options = optionsList.ToArray();
            _displayNames = _options.Select(o => o.Name).ToList();
        }

        // ============================================================
        // UI TOOLKIT IMPLEMENTATION
        // ============================================================
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            EnsureCache();

            var root = new VisualElement();
            
            // Container chứa các field con của object được chọn (sẽ clear và vẽ lại khi đổi type)
            var propertiesContainer = new VisualElement()
            {
                style = { marginLeft = 15, marginTop = 2 } // Thụt lề nhẹ cho đẹp
            };

            // 1. Tìm index hiện tại
            var currentType = GetManagedType(property);
            var currentIndex = 0;
            for (var i = 0; i < _options.Length; i++)
            {
                if (_options[i].Type == currentType)
                {
                    currentIndex = i;
                    break;
                }
            }

            // 2. Tạo Dropdown chọn Type
            var dropdown = new DropdownField(property.displayName, _displayNames, currentIndex)
            {
                style = { flexGrow = 1 }
            };

            // 3. Sự kiện khi chọn Type mới
            dropdown.RegisterValueChangedCallback(evt =>
            {
                // Tìm type tương ứng với tên được chọn
                var selectedIndex = _displayNames.IndexOf(evt.newValue);
                if (selectedIndex < 0) return;

                var selectedType = _options[selectedIndex].Type;

                // Nếu type khác với hiện tại thì tạo instance mới
                if (selectedType != GetManagedType(property))
                {
                    var newInstance = selectedType == null ? null : Activator.CreateInstance(selectedType);
                    property.managedReferenceValue = newInstance;
                    property.serializedObject.ApplyModifiedProperties();
                    
                    // Vẽ lại phần properties bên dưới
                    DrawProperties(property, propertiesContainer);
                }
            });

            // 4. Vẽ lần đầu
            DrawProperties(property, propertiesContainer);

            root.Add(dropdown);
            root.Add(propertiesContainer);

            return root;
        }

        // Hàm helper để vẽ các thuộc tính con (Children)
        private void DrawProperties(SerializedProperty property, VisualElement container)
        {
            container.Clear();

            if (property.managedReferenceValue == null) return;

            // Duyệt qua các field con của managedReference
            // Lưu ý: Cần copy property để iterator không ảnh hưởng property gốc
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = property.GetEndProperty();

            // Nhảy vào con đầu tiên (NextVisible(true) sẽ đi vào trong children)
            if (iterator.NextVisible(true))
            {
                do
                {
                    // Nếu đã đi hết các con của property này (chạm đến property kế tiếp ở level ngoài) thì dừng
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;

                    // Tạo PropertyField cho từng field con
                    var field = new PropertyField(iterator.Copy())
                    {
                        style = { marginBottom = 2 }
                    };
                    
                    // Bind dữ liệu để field tự update
                    field.Bind(property.serializedObject);
                    container.Add(field);

                } while (iterator.NextVisible(false)); // False = chỉ đi tiếp các field ngang hàng, không đi sâu vào cháu chắt
            }
        }

        // Helper lấy Type hiện tại an toàn
        private Type GetManagedType(SerializedProperty property)
        {
            return property.managedReferenceValue?.GetType();
        }
    }
}