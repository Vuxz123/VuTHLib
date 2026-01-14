using System;
using Core.GameCycle.Screen.GlobalEvent;

namespace Core.GameCycle.Screen.LocalEvents
{
    public interface ILocalScreenEventRegistration
    {
        void RegisterOnScreenOpening(Action<ScreenEventArgs> callback);
        void UnregisterOnScreenOpening(Action<ScreenEventArgs> callback);
        void RegisterOnScreenClosing(Action<ScreenEventArgs> callback);
        void UnregisterOnScreenClosing(Action<ScreenEventArgs> callback);
    }
}