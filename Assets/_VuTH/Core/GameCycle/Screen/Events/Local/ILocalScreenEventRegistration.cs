using System;
using _VuTH.Core.GameCycle.Screen.Events.Global;

namespace _VuTH.Core.GameCycle.Screen.Events.Local
{
    public interface ILocalScreenEventRegistration
    {
        void RegisterOnScreenOpening(Action<ScreenEventArgs> callback);
        void UnregisterOnScreenOpening(Action<ScreenEventArgs> callback);
        void RegisterOnScreenClosing(Action<ScreenEventArgs> callback);
        void UnregisterOnScreenClosing(Action<ScreenEventArgs> callback);
    }
}