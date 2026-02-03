using _VuTH.Core.GameCycle.Screen.Core.A;

namespace _VuTH.Core.GameCycle.Screen.Events.Local
{
    public interface IScreenMetaData
    {
        public string SceneName { get; }
        public IScreenDefinition ScreenDefinition { get; }
    }
}