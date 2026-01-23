using Core.Window.Transition;
using UnityEngine;

namespace Core.Window
{
    /// <summary>
    /// Modal popup - dialog with dimmed background.
    /// Use for confirmations, alerts, small dialogs.
    /// </summary>
    public abstract class PopupBase : UIViewBase,
        IWindowDefinition,
        ITransitionTarget,
        IBackgroundDimmer,
        ITransitionInputBlocker
    {
        public virtual WindowType WindowType => WindowType.Popup;
        public virtual UILayer Layer => UILayer.Popup;
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

        [Header("Transition Runtime Hooks (Optional)")]
        [Tooltip("If set, transitions animate this RectTransform instead of the root RectTransform.")]
        [SerializeField] private RectTransform transitionRoot;

        [Tooltip("If set, FadeTransition will call SetDim(alpha) to drive this CanvasGroup alpha.")]
        [SerializeField] private CanvasGroup dimmerCanvasGroup;

        public virtual bool BlockInput => true;

        /// <summary>
        /// Whether this popup can be closed by back button.
        /// Override to false for important confirmations.
        /// </summary>
        public virtual bool AllowBackClose => true;

        public bool CloseOnBackPress => AllowBackClose;

        /// <summary>
        /// Whether clicking outside the popup should close it.
        /// </summary>
        public virtual bool CloseOnOutsideClick => false;

        public override void OnBackPressed()
        {
            if (AllowBackClose)
            {
                Close();
            }
        }

        /// <summary>
        /// Target for transitions.
        /// If not set, TransitionUtils will fall back to this view's RectTransform.
        /// </summary>
        public RectTransform TransitionRoot => transitionRoot;

        /// <summary>
        /// Called by FadeTransition (and any other dimmer-aware transitions).
        /// </summary>
        public void SetDim(float alpha)
        {
            if (dimmerCanvasGroup == null)
                return;

            dimmerCanvasGroup.alpha = Mathf.Clamp01(alpha);
            // Usually the dimmer should block raycasts when visible.
            dimmerCanvasGroup.blocksRaycasts = alpha > 0.001f;
        }

        /// <summary>
        /// Called by transitions to block input for this specific view.
        /// WindowManager may also block globally.
        /// </summary>
        public void SetInputBlocked(bool blocked)
        {
            if (CanvasGroup == null)
                return;

            CanvasGroup.interactable = !blocked;
            CanvasGroup.blocksRaycasts = !blocked;
        }
    }

    /// <summary>
    /// Popup with typed result
    /// </summary>
    public abstract class PopupBase<TResult> : PopupBase
    {
        protected void Close(TResult result)
        {
            CloseSource?.TrySetResult(result);
        }
    }
}