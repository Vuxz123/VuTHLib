namespace Core.Window
{
    /// <summary>
    /// Implement this on a root object inside a Screen scene so other systems (e.g. ScreenManager) can
    /// locate the Screen-scoped WindowManager instance.
    /// </summary>
    public interface IScreenWindowProvider
    {
        IWindowManager WindowManager { get; }
    }
}
