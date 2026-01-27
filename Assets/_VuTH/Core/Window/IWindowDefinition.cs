namespace _VuTH.Core.Window
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

        /// <summary>
        /// Legacy preset fallback used when TransitionIn/OutSettings are null.
        /// Applies to both In and Out.
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

        bool BlockInput { get; }
        bool CloseOnBackPress { get; }
    }
}
