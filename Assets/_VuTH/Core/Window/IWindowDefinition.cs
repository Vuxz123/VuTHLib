namespace Core.Window
{
    /// <summary>
    /// Optional per-window defaults.
    /// Implemented by base classes (PopupBase/FullScreenBase) so WindowManager can derive
    /// defaults without requiring an external UIViewConfig.
    /// </summary>
    public interface IWindowDefinition
    {
        WindowType WindowType { get; }
        UILayer Layer { get; }
        string TransitionPreset { get; }
        bool BlockInput { get; }
        bool CloseOnBackPress { get; }
    }
}
