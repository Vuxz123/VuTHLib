using UnityEngine.UIElements;

namespace Common.Editor.Settings.Util
{
    public class SettingSection : VisualElement
    {
        public SettingSection(string title)
        {
            Add(new Label(title));
        }
    }
}