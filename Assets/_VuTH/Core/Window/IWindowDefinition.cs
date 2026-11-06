namespace _VuTH.Core.Window
{
    /// <summary>
    /// Optional per-window defaults, implemented by <see cref="PopupBase"/> and <see cref="FullScreenBase"/>.
    /// Values set in <see cref="WindowOptions"/> take precedence.
    /// </summary>
    public interface IWindowDefinition
    {
        WindowType WindowType { get; }

        /// <summary>
        /// Preset fallback used when TransitionIn/OutSettings are null. Applies to both In and Out.
        /// </summary>
        string TransitionPreset { get; }

        /// <summary>
        /// Data-driven transition settings for Open.
        /// If null, falls back to TransitionPreset.
        /// </summary>
        Transition.UITransitionSettings TransitionInSettings { get; }

        /// <summary>
        /// Data-driven transition settings for Close.
        /// If null, falls back to TransitionPreset.
        /// </summary>
        Transition.UITransitionSettings TransitionOutSettings { get; }

        /// <summary>Whether input is blocked while the window transitions in / out.</summary>
        bool BlockInput { get; }

        /// <summary>Whether the back button / Escape reaches the window (<see cref="UIViewBase.OnBackPressed"/>).</summary>
        bool CloseOnBackPress { get; }

        /// <summary>What opening this window does when one of the same type is already open.</summary>
        WindowDuplicatePolicy DuplicatePolicy { get; }

        /// <summary>Whether the window stays open when the Screen changes (see <see cref="IWindowManager.CloseForScreenChangeAsync"/>).</summary>
        bool KeepOnScreenChange { get; }

        /// <summary>
        /// Whether the closed window is kept (deactivated) and reused by its next opening, keeping its state (scroll
        /// position, selected tab…), instead of returning to the pool.
        /// </summary>
        bool KeepInstance { get; }

        /// <summary>Whether a click outside the window (on its dimmer) closes it. Only popups have one.</summary>
        bool CloseOnOutsideClick { get; }
    }
}
