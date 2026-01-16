using Common.Editor.Settings;
using UnityEngine.UIElements;

namespace Core.GameCycle.ScreenFlow.Editor.Setting
{
    [SettingsTab("ScreenFlow", "Screen Flow", 20)]
    public class ScreenFlowSettingTab : ISettingsTab
    {
        public string Id => "ScreenFlow";
        public string Title => "Screen Flow";
        public int Order => 20;

        public VisualElement CreateView()
        {
            var container = new VisualElement();
            container.Add(new Label("Welcome to the Screen Flow settings!"));
            return container;
        }
    }
}