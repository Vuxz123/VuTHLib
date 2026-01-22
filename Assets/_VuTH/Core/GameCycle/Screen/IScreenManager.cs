using Core.GameCycle.Screen.LocalEvents;
using Cysharp.Threading.Tasks;
using System;
using Common;

namespace Core.GameCycle.Screen
{
    public interface IScreenManager : ICommonManager
    {
        // Events
        ILocalScreenEventRegistration LocalEventRegistration { get; }

        /// <summary>
        /// Fired after a transition has actually completed (not fired for ignored calls).
        /// Kind describes what happened (Enter/Push/Pop/Override), while Context describes origin/why.
        /// </summary>
        event Action<TransitionCompletedEventArgs> OnTransitionCompleted;

        // State
        ScreenModel Current { get; }
        ScreenModel Previous { get; }
        bool IsTransitioning { get; }

        // Core API
        UniTask Enter(ScreenModel screen);
        UniTask Enter(ScreenModel screen, TransitionContext context);

        UniTask Enter(ScreenIdentifier screenID);
        UniTask Enter(ScreenIdentifier screenID, TransitionContext context);

        // Navigation (runtime)
        UniTask Push(ScreenModel screen);
        UniTask Push(ScreenModel screen, TransitionContext context);

        UniTask Pop();
        UniTask Pop(TransitionContext context);
        bool CanPop { get; }

        // Override / Interrupt
        UniTask PushOverride(ScreenModel screen);
        UniTask PushOverride(ScreenModel screen, TransitionContext context);

        UniTask PopOverride();
        UniTask PopOverride(TransitionContext context);
        bool IsOverriding { get; }
    }
}