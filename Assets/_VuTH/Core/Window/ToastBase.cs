using _VuTH.Core.Window.Transition;
using UnityEngine;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Short non-blocking message (toast / notification), shown with <see cref="IWindowManager.ShowToast{TToast}"/> and
    /// closed after <see cref="Duration"/>.
    /// <para>
    /// A toast is a <see cref="WindowType.System"/> window: it never blocks input (not even during its transitions),
    /// never takes the back button, does not hold back queued windows and stays open across Screen changes. Several
    /// toasts may be open at once; position them in <see cref="UIViewBase.Setup"/> if they must not overlap.
    /// </para>
    /// </summary>
    public abstract class ToastBase : UIViewBase, IWindowDefinition, ITransitionTarget
    {
        [Header("Toast")]
        [Tooltip("Seconds (unscaled) before the toast closes, unless ShowToast passes a duration.")]
        [SerializeField, Min(0f)] private float duration = 2f;

        [Tooltip("If set, transitions animate this RectTransform instead of the root RectTransform.")]
        [SerializeField] private RectTransform transitionRoot;

        [Header("Transition Settings (Optional)")]
        [SerializeReference]
        [TransitionSettingsReference]
        private UITransitionSettings transitionIn;

        [SerializeReference]
        [TransitionSettingsReference]
        private UITransitionSettings transitionOut;

        /// <summary>Seconds (unscaled) the toast stays shown.</summary>
        public virtual float Duration => duration;

        /// <summary>Whether the toast receives clicks once shown (e.g. a "tap to open" toast). Default: false.</summary>
        public virtual bool Interactable => false;

        public RectTransform TransitionRoot => transitionRoot;

        public WindowType WindowType => WindowType.System;
        public virtual string TransitionPreset => "Fade";
        public UITransitionSettings TransitionInSettings => transitionIn;
        public UITransitionSettings TransitionOutSettings => transitionOut;
        public bool BlockInput => false;
        public bool CloseOnBackPress => false;
        public WindowDuplicatePolicy DuplicatePolicy => WindowDuplicatePolicy.Allow;
        public virtual bool KeepOnScreenChange => true;
        public bool KeepInstance => false;

        public override void OnViewShown()
        {
            if (Interactable) return;

            // Transitions end interactable; a toast must not swallow the clicks meant for the game below.
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
        }
    }
}
