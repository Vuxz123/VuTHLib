// filepath: c:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Common\Editor\Field\DictionaryDrawerAttribute.cs

using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Optional attribute: put [Dictionary] on a SerializableDictionary field to force the custom drawer
    /// in case your Unity version doesn't apply the open-generic type drawer automatically.
    /// </summary>
    [CustomPropertyDrawer(typeof(DictionaryAttribute))]
    public class DictionaryAttributeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DictionaryField.Draw(property, label);
            EditorGUI.EndProperty();
        }
    }
}
