namespace Core.GameCycle.Screen.LocalEvents
{
    public interface IScreenMetaData
    {
        public string SceneName { get; }
        public IScreenDefinition ScreenDefinition { get; }
    }
}