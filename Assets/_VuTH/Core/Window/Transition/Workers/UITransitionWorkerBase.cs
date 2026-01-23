namespace Core.Window.Transition.Workers
{
    /// <summary>
    /// Worker contract for mapping a preset name to a transition instance.
    /// </summary>
    public abstract class UITransitionWorkerBase
    {
        /// <summary>
        /// Whether this worker can create a transition for the given preset.
        /// </summary>
        public abstract bool CanHandle(string presetName);

        /// <summary>
        /// Create a new transition instance.
        /// </summary>
        public abstract IUITransition Create(string presetName);
    }
}

