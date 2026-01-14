using Common.SharedLib.Scene;

namespace Core.GameCycle.Screen
{
    public interface IScreenDefinition
    {
        // ReSharper disable once InconsistentNaming
        public ScreenIdentifier ScreenID { get; }
    }
}