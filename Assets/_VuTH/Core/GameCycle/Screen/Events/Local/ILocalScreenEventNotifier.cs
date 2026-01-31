using _VuTH.Core.GameCycle.Screen.Events.Global;

namespace _VuTH.Core.GameCycle.Screen.Events.Local
{
    public interface ILocalScreenEventNotifier
    {
        void NotifyScreenOpening(ScreenEventArgs args);
        void NotifyScreenClosing(ScreenEventArgs args);
    }
}