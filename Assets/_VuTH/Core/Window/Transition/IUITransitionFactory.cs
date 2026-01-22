namespace Core.Window.Transition
{
    /// <summary>
    /// Factory for creating transitions
    /// </summary>
    public interface IUITransitionFactory
    {
        IUITransition Create(string presetName);
    }
}