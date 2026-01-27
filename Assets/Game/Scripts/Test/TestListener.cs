using System;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.GlobalEvent;

namespace Game.Scripts.Test
{
    [Serializable]
    public class TestListener : IScreenEventListener
    {
        public void OnPreScreenEnter(ScreenEventArgs eventArgs)
        {
            this.Log("OnPreScreenEnter");
        }

        public void OnPostScreenEnter(ScreenEventArgs eventArgs)
        {
            this.Log("OnPostScreenEnter");
        }

        public void OnPreScreenExit(ScreenEventArgs eventArgs)
        {
            this.Log("OnPreScreenExit");
        }

        public void OnPostScreenExit(ScreenEventArgs eventArgs)
        {
            this.Log("OnPostScreenExit");
        }
    }
}