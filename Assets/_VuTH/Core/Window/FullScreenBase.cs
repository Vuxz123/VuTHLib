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
        public virtual WindowType WindowType => WindowType.FullScreenPopup;

        /// <summary>Preset used for open / close when no transition settings are set.</summary>
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

        /// <summary>Whether input is blocked while the window transitions in / out.</summary>
        public virtual bool BlockInput => true;

        /// <summary>
        /// Whether this popup can be closed by back button.
        /// Override to customize behavior.
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
    }

    /// <summary>Full screen window with typed result.</summary>
    public abstract class FullScreenBase<TResult> : FullScreenBase
    {
        protected void Close(TResult result)
        {
            base.Close(result);
        }
    }

    /// <summary>
    /// Full screen window with typed data and result: open it with
    /// <c>windowManager.Open&lt;TWindow, TData, TResult&gt;(data)</c>, and receive the data in <see cref="OnSetup"/>.
    /// </summary>
    public abstract class FullScreenBase<TData, TResult> : FullScreenBase<TResult>, ITypedWindow<TData, TResult>
    {
        public sealed override void Setup(object data)
        {
            OnSetup(TypedWindowData.Cast<TData>(this, data));
        }

        /// <summary>Receives the data passed to Open, before the window is shown.</summary>
        protected virtual void OnSetup(TData data) { }
    }
}
