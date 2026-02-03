using System;
using _VuTH.Core.GameCycle.Screen.Core.A;

namespace _VuTH.Core.GameCycle.Screen.Events.Global
{
    public interface IScreenEventListener
    {
        public static class Priority
        {
            public const int Highest = 100;
            public const int Higher = 75;
            public const int High = 50;
            public const int Normal = 0;
            public const int Low = -50;
            public const int Lower = -75;
            public const int Lowest = -100;
        }
        
        public void OnPreScreenEnter(ScreenEventArgs eventArgs);
        public void OnPostScreenEnter(ScreenEventArgs eventArgs);
        public void OnPreScreenExit(ScreenEventArgs eventArgs);
        public void OnPostScreenExit(ScreenEventArgs eventArgs);
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public class ScreenEventListenerPriority : Attribute
    {
        public int Priority { get; }

        public ScreenEventListenerPriority(int priority)
        {
            Priority = priority;
        }
    }

    public struct ScreenEventArgs
    {
        public IScreenDefinition FromScreen { get; }
        public IScreenDefinition ToScreen { get; }

        public ScreenEventArgs(IScreenDefinition fromScreen, IScreenDefinition toScreen)
        {
            FromScreen = fromScreen;
            ToScreen = toScreen;
        }
    }
}