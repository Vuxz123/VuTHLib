using UnityEngine.UIElements;

namespace Common.Editor.Settings
{
    public interface ISettingsTab
    {
        /// Unique, stable id
        string Id { get; }

        /// Display name
        string Title { get; }

        /// Order in sidebar
        int Order { get; }

        /// Root visual của tab
        VisualElement CreateView();
    }
}