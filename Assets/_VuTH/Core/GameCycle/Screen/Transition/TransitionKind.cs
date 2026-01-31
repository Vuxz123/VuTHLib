namespace _VuTH.Core.GameCycle.Screen.Transition
{
    /// <summary>
    /// Describes the navigation form (what happened), not the origin/why.
    /// Origin/why should be expressed via TransitionContext/metadata.
    /// </summary>
    public enum TransitionKind
    {
        Enter = 0,
        Push = 1,
        Pop = 2,
        PushOverride = 3,
        PopOverride = 4
    }
}

