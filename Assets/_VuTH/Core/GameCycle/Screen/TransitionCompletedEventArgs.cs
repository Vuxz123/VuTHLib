namespace Core.GameCycle.Screen
{
    public readonly struct TransitionCompletedEventArgs
    {
        public TransitionCompletedEventArgs(TransitionKind kind, ScreenModel from, ScreenModel to, TransitionContext context)
        {
            Kind = kind;
            From = from;
            To = to;
            Context = context;
        }

        public TransitionKind Kind { get; }
        public ScreenModel From { get; }
        public ScreenModel To { get; }
        public TransitionContext Context { get; }

        public override string ToString() => $"{Kind}: {From?.name} -> {To?.name} ({Context})";
    }
}

