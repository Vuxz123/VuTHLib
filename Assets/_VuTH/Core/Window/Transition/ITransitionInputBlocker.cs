namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Optional capability: view-level input blocking hook for transitions.
    /// WindowManager already has global blocking; this is for per-view needs.
    /// </summary>
    public interface ITransitionInputBlocker
    {
        void SetInputBlocked(bool blocked);
    }
}

