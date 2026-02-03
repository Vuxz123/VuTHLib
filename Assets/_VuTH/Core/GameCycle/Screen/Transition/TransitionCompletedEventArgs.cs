using _VuTH.Core.GameCycle.Screen.Core.A;

namespace _VuTH.Core.GameCycle.Screen.Transition
{
    public readonly struct TransitionCompletedEventArgs
    {
        public TransitionCompletedEventArgs(TransitionKind kind, IScreenDefinition from, IScreenDefinition to, TransitionContext context)
        {
            Kind = kind;
            From = from;
            To = to;
            Context = context;
        }

        public TransitionKind Kind { get; }
        public IScreenDefinition From { get; }
        public IScreenDefinition To { get; }
        public TransitionContext Context { get; }

        public override string ToString() => $"{Kind}: {From?.ScreenID.name} -> {To?.ScreenID.name} ({Context})";
    }
}

