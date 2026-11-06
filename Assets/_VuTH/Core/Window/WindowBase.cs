using _VuTH.Core.Window.Transition;
using UnityEngine;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Shared base of <see cref="PopupBase"/> and <see cref="FullScreenBase"/>: the per-window defaults
    /// (<see cref="IWindowDefinition"/>) and the optional transition settings. Every value can still be overridden per
    /// opening with <see cref="WindowOptions"/>.
    /// </summary>
    public abstract class WindowBase : UIViewBase, IWindowDefinition
    {
        [Header("Transition Settings (Optional)")]
        [SerializeReference]
        [TransitionSettingsReference]
        private UITransitionSettings transitionIn;

        [SerializeReference]
        [TransitionSettingsReference]
        private UITransitionSettings transitionOut;

        public abstract WindowType WindowType { get; }

        /// <summary>Preset used for a direction without transition settings.</summary>
        public virtual string TransitionPreset => WindowOptions.DefaultTransitionPreset;

        public UITransitionSettings TransitionInSettings => transitionIn;
        public UITransitionSettings TransitionOutSettings => transitionOut;

        /// <summary>Whether input is blocked while the window transitions in / out.</summary>
        public virtual bool BlockInput => true;

        /// <summary>
        /// Whether the back button reaches this window (and closes it, unless <see cref="UIViewBase.OnBackPressed"/>
        /// is overridden). Override to false for confirmations that must be answered.
        /// </summary>
        public virtual bool AllowBackClose => true;

        public bool CloseOnBackPress => AllowBackClose;

        /// <summary>
        /// What opening this window does when one of the same type is already open. Override with
        /// <see cref="WindowDuplicatePolicy.Ignore"/> to prevent double-tap duplicates.
        /// </summary>
        public virtual WindowDuplicatePolicy DuplicatePolicy => WindowDuplicatePolicy.Allow;

        /// <summary>Whether this window stays open when the Screen changes.</summary>
        public virtual bool KeepOnScreenChange => false;

        /// <summary>Whether the closed instance is kept and reused by the next opening, keeping its state.</summary>
        public virtual bool KeepInstance => false;

        /// <summary>Whether a click outside the window (on its dimmer) closes it. Only <see cref="PopupBase"/> has one.</summary>
        public virtual bool CloseOnOutsideClick => false;
    }
}
