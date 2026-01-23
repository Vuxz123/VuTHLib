using Core.Window.Transition;

namespace Core.Window
{
    /// <summary>
    /// Window open options
    /// </summary>
    public class WindowOptions
    {
        public object Data { get; set; }
        public WindowType WindowType { get; set; } = WindowType.Popup;

        /// <summary>
        /// Legacy preset name fallback used when TransitionIn/OutSettings are null.
        /// Applies to both In and Out.
        /// </summary>
        public string TransitionPreset { get; set; } = "Scale";

        /// <summary>
        /// Data-driven transition for Open.
        /// If null, falls back to TransitionPreset.
        /// </summary>
        public UITransitionSettings TransitionInSettings { get; set; }

        /// <summary>
        /// Data-driven transition for Close.
        /// If null, falls back to TransitionPreset.
        /// </summary>
        public UITransitionSettings TransitionOutSettings { get; set; }

        public bool? CloseOnBackPress { get; set; }
        public bool? BlockInput { get; set; }
    }
}