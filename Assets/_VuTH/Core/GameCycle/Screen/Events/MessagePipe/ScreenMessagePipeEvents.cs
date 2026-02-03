using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Core.GameCycle.Screen.Core.A;
using _VuTH.Core.GameCycle.Screen.Transition;

namespace _VuTH.Core.GameCycle.Screen.Events.MessagePipe
{
    /// <summary>
    /// Base event args for screen lifecycle events.
    /// </summary>
    public abstract class ScreenMessagePipeEventArgs
    {
        public IScreenDefinition FromScreen { get; }
        public IScreenDefinition ToScreen { get; }

        protected ScreenMessagePipeEventArgs(IScreenDefinition fromScreen, IScreenDefinition toScreen)
        {
            FromScreen = fromScreen;
            ToScreen = toScreen;
        }
    }

    /// <summary>
    /// Event published before a screen enters.
    /// Use this for setup/initialization before the screen becomes active.
    /// </summary>
    [MessagePipeEvent]
    public class PreScreenEnterEvent : ScreenMessagePipeEventArgs
    {
        public PreScreenEnterEvent(IScreenDefinition fromScreen, IScreenDefinition toScreen) 
            : base(fromScreen, toScreen)
        {
        }
    }

    /// <summary>
    /// Event published after a screen has entered and is fully active.
    /// Use this for post-activation logic.
    /// </summary>
    [MessagePipeEvent]
    public class PostScreenEnterEvent : ScreenMessagePipeEventArgs
    {
        public PostScreenEnterEvent(IScreenDefinition fromScreen, IScreenDefinition toScreen) 
            : base(fromScreen, toScreen)
        {
        }
    }

    /// <summary>
    /// Event published before a screen exits.
    /// Use this for cleanup/save operations before the screen becomes inactive.
    /// </summary>
    [MessagePipeEvent]
    public class PreScreenExitEvent : ScreenMessagePipeEventArgs
    {
        public PreScreenExitEvent(IScreenDefinition fromScreen, IScreenDefinition toScreen) 
            : base(fromScreen, toScreen)
        {
        }
    }

    /// <summary>
    /// Event published after a screen has exited and is no longer active.
    /// Use this for post-deactivation cleanup.
    /// </summary>
    [MessagePipeEvent]
    public class PostScreenExitEvent : ScreenMessagePipeEventArgs
    {
        public PostScreenExitEvent(IScreenDefinition fromScreen, IScreenDefinition toScreen) 
            : base(fromScreen, toScreen)
        {
        }
    }

    /// <summary>
    /// Event published when screen transition completes.
    /// Contains additional context about the transition type.
    /// </summary>
    [MessagePipeEvent]
    public class ScreenTransitionCompletedEvent
    {
        public TransitionKind Kind { get; }
        public IScreenDefinition FromScreen { get; }
        public IScreenDefinition ToScreen { get; }
        public TransitionContext Context { get; }

        public ScreenTransitionCompletedEvent(TransitionKind kind, IScreenDefinition fromScreen, 
            IScreenDefinition toScreen, TransitionContext context)
        {
            Kind = kind;
            FromScreen = fromScreen;
            ToScreen = toScreen;
            Context = context;
        }
    }
}
