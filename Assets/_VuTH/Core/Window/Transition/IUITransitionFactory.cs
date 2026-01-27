namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Factory for creating transitions
    /// </summary>
    public interface IUITransitionFactory
    {
        /// <summary>
        /// Data-driven creation (preferred).
        /// </summary>
        IUITransition Create(UITransitionSettings settings);

        /// <summary>
        /// Legacy preset-based creation.
        /// </summary>
        IUITransition Create(string presetName);
    }
}