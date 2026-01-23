namespace Core.Window.Transition
{
    /// <summary>
    /// Optional capability: allows a view to control a dimmed background while transitioning.
    /// </summary>
    public interface IBackgroundDimmer
    {
        void SetDim(float alpha);
    }
}

