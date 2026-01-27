using System;
using _VuTH.Core.GameCycle.Screen.GlobalEvent;

namespace _VuTH.Core.GameCycle.Screen.LocalEvents
{
    public class LocalScreenEventContainer : ILocalScreenEventRegistration, ILocalScreenEventNotifier
    {
        private event Action<ScreenEventArgs> _onScreenOpening;
        private event Action<ScreenEventArgs> _onScreenClosing;
        
        public void RegisterOnScreenOpening(Action<ScreenEventArgs> callback)
        {
            _onScreenOpening += callback;
        }
        
        public void UnregisterOnScreenOpening(Action<ScreenEventArgs> callback)
        {
            _onScreenOpening -= callback;
        }
        
        public void RegisterOnScreenClosing(Action<ScreenEventArgs> callback)
        {
            _onScreenClosing += callback;
        }
        
        public void UnregisterOnScreenClosing(Action<ScreenEventArgs> callback)
        {
            _onScreenClosing -= callback;
        }
        
        public void NotifyScreenOpening(ScreenEventArgs args)
        {
            _onScreenOpening?.Invoke(args);
        }
        
        public void NotifyScreenClosing(ScreenEventArgs args)
        {
            _onScreenClosing?.Invoke(args);
        }
    }
}