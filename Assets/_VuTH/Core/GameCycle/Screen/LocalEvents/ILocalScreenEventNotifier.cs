using Core.GameCycle.Screen.GlobalEvent;

namespace Core.GameCycle.Screen.LocalEvents
{
    public interface ILocalScreenEventNotifier
    {
        void NotifyScreenOpening(ScreenEventArgs args);
        void NotifyScreenClosing(ScreenEventArgs args);
    }
}