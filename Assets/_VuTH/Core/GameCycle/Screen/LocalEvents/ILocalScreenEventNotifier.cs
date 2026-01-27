using _VuTH.Core.GameCycle.Screen.GlobalEvent;

namespace _VuTH.Core.GameCycle.Screen.LocalEvents
{
    public interface ILocalScreenEventNotifier
    {
        void NotifyScreenOpening(ScreenEventArgs args);
        void NotifyScreenClosing(ScreenEventArgs args);
    }
}