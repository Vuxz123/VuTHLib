using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Optional attribute drawer: put [Dictionary] on a SerializableDictionary field to force the custom drawer
    /// in case your Unity version doesn't apply the open-generic type drawer automatically.
    /// </summary>
    [CustomPropertyDrawer(typeof(DictionaryAttribute))]
    public class DictionaryAttributeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => DictionaryField.GetHeight(property, label);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            DictionaryField.Draw(position, property, label);
            EditorGUI.EndProperty();
        }
    }
}
