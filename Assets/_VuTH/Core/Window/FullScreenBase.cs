using _VuTH.Core.Window.Transition;
using UnityEngine;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Full screen popup - blocks interaction with content below.
    /// Use for dialogs that need full user attention (e.g., settings, inventory).
    /// </summary>
    public abstract class FullScreenBase : UIViewBase, IWindowDefinition
    {
        /// <summary>
        /// FullScreenBase mặc định là FullScreenPopup và nằm trên layer Popup.
        /// </summary>
        public virtual WindowType WindowType => WindowType.FullScreenPopup;
        public virtual UILayer Layer => UILayer.Popup;

        /// <summary>
        /// Preset transition dùng cho open/hide.
        /// </summary>
        public virtual string TransitionPreset => "Scale";

        [Header("Transition Settings (Optional)")]
        [SerializeReference]
        [TransitionSettingsReference]
        private UITransitionSettings transitionIn;

        [SerializeReference]
        [TransitionSettingsReference]
        private UITransitionSettings transitionOut;

        public UITransitionSettings TransitionInSettings => transitionIn;
        public UITransitionSettings TransitionOutSettings => transitionOut;

        /// <summary>
        /// Fullscreen popup thường block input trong transition.
        /// </summary>
        public virtual bool BlockInput => true;

        /// <summary>
        /// Whether this popup blocks interaction with content below.
        /// Default: true for full screen popups.
        /// </summary>
        public virtual bool BlocksInteraction => true;
         
        /// <summary>
        /// Whether this popup can be closed by back button.
        /// Override to customize behavior.
        /// </summary>
        public virtual bool AllowBackClose => true;

        /// <summary>
        /// Mapping cho WindowManager.
        /// </summary>
        public bool CloseOnBackPress => AllowBackClose;
         
        public override void OnBackPressed()
        {
            if (AllowBackClose)
            {
                Close();
            }
        }
    }
}