using _VuTH.Common.Log;
using _VuTH.Core.Window.Transition;
using _VuTH.Core.Window.UI;
using UnityEngine;
using UnityEngine.UI;

namespace _VuTH.Core.Window
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

        [Tooltip("Clicks on this graphic count as outside the popup (CloseOnOutsideClick). Default: the dimmer's graphic.")]
        [SerializeField] private Graphic outsideClickArea;

        /// <summary>Whether a click outside the popup (on its dimmer) closes it. Default: false.</summary>
        public virtual bool CloseOnOutsideClick => false;

        protected override void Awake()
        {
            base.Awake();
            SetUpOutsideClick();
        }

        private void SetUpOutsideClick()
        {
            if (!CloseOnOutsideClick) return;

            var area = outsideClickArea;
            if (!area && dimmerCanvasGroup) area = dimmerCanvasGroup.GetComponent<Graphic>();
            if (!area)
            {
                this.LogWarning($"{name}: CloseOnOutsideClick needs an outside click area or a dimmer with a Graphic.");
                return;
            }

            area.raycastTarget = true;
            if (!area.TryGetComponent<WindowOutsideClickCatcher>(out var catcher))
                catcher = area.gameObject.AddComponent<WindowOutsideClickCatcher>();
            catcher.Clicked = OnOutsideClick;
        }

        /// <summary>Called on a click outside the popup. Default: closes it, like the back button.</summary>
        protected virtual void OnOutsideClick()
        {
            if (CloseOnOutsideClick) Close();
        }

        public virtual bool BlockInput => true;

        /// <summary>
        /// Whether this popup can be closed by back button.
        /// Override to false for important confirmations.
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

        public override void OnBackPressed()
        {
            if (TryHandleBackInChildren()) return;
            if (AllowBackClose) Close();
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
            base.Close(result);
        }
    }

    /// <summary>
    /// Popup with typed data and result: open it with <c>windowManager.Open&lt;TWindow, TData, TResult&gt;(data)</c>
    /// for compile-time checked types, and receive the data in <see cref="OnSetup"/>.
    /// </summary>
    public abstract class PopupBase<TData, TResult> : PopupBase<TResult>, ITypedWindow<TData, TResult>
    {
        public sealed override void Setup(object data)
        {
            OnSetup(TypedWindowData.Cast<TData>(this, data));
        }

        /// <summary>Receives the data passed to Open, before the window is shown.</summary>
        protected virtual void OnSetup(TData data) { }
    }
}
