using Common.SharedLib;
using Core.GameCycle.Screen.LocalEvents;
using Cysharp.Threading.Tasks;

namespace Core.GameCycle.Screen
{
    public interface IScreenManager : ICommonManager
    {
        // Events
        ILocalScreenEventRegistration LocalEventRegistration { get; }

        // State
        ScreenModel Current { get; }
        ScreenModel Previous { get; }
        bool IsTransitioning { get; }

        // Core API
        UniTask Enter(ScreenModel screen);
        UniTask Enter(ScreenIdentifier screenID);

        // Navigation (runtime)
        UniTask Push(ScreenModel screen);
        UniTask Pop();
        bool CanPop { get; }

        // Override / Interrupt
        UniTask PushOverride(ScreenModel screen);
        UniTask PopOverride();
        bool IsOverriding { get; }
    }
}