using _VuTH.Core.GameCycle.Screen.Identifier;

namespace _VuTH.Core.GameCycle.Screen.Core
{
    public interface IScreenDefinition
    {
        // ReSharper disable once InconsistentNaming
        public ScreenIdentifier ScreenID { get; }
    }
}