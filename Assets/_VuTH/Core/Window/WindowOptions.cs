using _VuTH.Core.Window.Transition;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Window open options. Every field left null takes the window's default (its <see cref="IWindowDefinition"/>),
    /// then the built-in default; a value set here always wins.
    /// </summary>
    public class WindowOptions
    {
        /// <summary>Preset used when neither the caller nor the window chooses a transition.</summary>
        public const string DefaultTransitionPreset = "Scale";

        /// <summary>Passed to <see cref="UIViewBase.Setup"/>.</summary>
        public object Data { get; set; }

        /// <summary>Sorting layer of the window. Default: the window's, else <see cref="Window.WindowType.Popup"/>.</summary>
        public WindowType? WindowType { get; set; }

        /// <summary>
        /// Preset name used for a direction without transition settings (applies to both In and Out).
        /// Default: the window's, else <see cref="DefaultTransitionPreset"/>.
        /// </summary>
        public string TransitionPreset { get; set; }

        /// <summary>Data-driven transition for Open. If null, falls back to the window's, then to the preset.</summary>
        public UITransitionSettings TransitionInSettings { get; set; }

        /// <summary>Data-driven transition for Close. If null, falls back to the window's, then to the preset.</summary>
        public UITransitionSettings TransitionOutSettings { get; set; }

        /// <summary>Whether the back button / Escape reaches the window. Default: the window's, else true.</summary>
        public bool? CloseOnBackPress { get; set; }

        /// <summary>Whether input is blocked while the window transitions in / out. Default: the window's, else true.</summary>
        public bool? BlockInput { get; set; }

        /// <summary>
        /// What to do when a window of the same type is already open.
        /// Default: the window's, else <see cref="WindowDuplicatePolicy.Allow"/>.
        /// </summary>
        public WindowDuplicatePolicy? Duplicate { get; set; }

        /// <summary>Whether the window stays open when the Screen changes. Default: the window's, else false.</summary>
        public bool? KeepOnScreenChange { get; set; }

        /// <summary>
        /// Whether the closed instance is kept and reused by the next opening (state preserved). Default: the
        /// window's, else false.
        /// </summary>
        public bool? KeepInstance { get; set; }

        /// <summary>Whether a click outside the popup (on its dimmer) closes it. Default: the window's, else false.</summary>
        public bool? CloseOnOutsideClick { get; set; }

        /// <summary>A copy, so resolving never changes an options object the caller may reuse.</summary>
        public WindowOptions Clone() => (WindowOptions)MemberwiseClone();
    }
}
