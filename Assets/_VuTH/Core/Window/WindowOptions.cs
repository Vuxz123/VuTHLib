namespace Core.Window
{
    /// <summary>
    /// Window open options
    /// </summary>
    public class WindowOptions
    {
        public object Data { get; set; }
        public WindowType WindowType { get; set; } = WindowType.Popup;
        public string TransitionPreset { get; set; } = "Scale";
        public bool CloseOnBackPress { get; set; } = true;
        public bool BlockInput { get; set; } = true;
    }
}