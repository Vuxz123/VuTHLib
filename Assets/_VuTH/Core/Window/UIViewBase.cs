using System;
using _VuTH.Core.Window.Transition;
using UnityEngine;
using UnityEngine.UI;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Base class for all UI views opened by <see cref="IWindowManager"/>.
    /// <para>
    /// Lifecycle of one opening: <see cref="Setup"/> → transition in → <see cref="OnViewShown"/> → … →
    /// <see cref="Close"/> → transition out → <see cref="OnViewHidden"/> → back to the pool. The same instance is
    /// reused for later openings.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class UIViewBase : MonoBehaviour, IUIView
    {
        public GameObject GameObject => gameObject;
        public Canvas Canvas { get; private set; }
        public CanvasGroup CanvasGroup { get; private set; }

        private Action<object> _requestClose;
        private Action _forceClose;

        // Transform of the transition root at rest, restored before each opening: transitions leave it off screen /
        // scaled to zero when the window closes.
        private RectTransform _restRoot;
        private Vector3 _restScale;
        private Vector2 _restAnchoredPosition;

        /// <summary>Whether the window is currently opened by the window manager.</summary>
        public bool IsOpen => _requestClose != null;

        /// <summary>
        /// Options of the current opening, resolved (every field set: caller, then window, then defaults). Null while
        /// the window is not open.
        /// </summary>
        protected WindowOptions Options { get; private set; }

        protected virtual void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (!Canvas) Canvas = GetComponent<Canvas>();
            if (!Canvas) Canvas = gameObject.AddComponent<Canvas>();
            if (!CanvasGroup) CanvasGroup = GetComponent<CanvasGroup>();
            if (!CanvasGroup) CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            // A canvas that overrides sorting only receives clicks through its own raycaster.
            if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
        }

        /// <summary>Receives the data passed to Open, before the window is shown.</summary>
        public virtual void Setup(object data) { }

        /// <summary>Called once the window finished transitioning in.</summary>
        public virtual void OnViewShown() { }

        /// <summary>Called once the window finished transitioning out, before it returns to the pool.</summary>
        public virtual void OnViewHidden() { }

        /// <summary>
        /// Called on back button / Escape when this is the top window. Default: lets an active
        /// <see cref="IWindowBackHandler"/> child consume it (e.g. a tab group), else <see cref="Close"/>.
        /// </summary>
        public virtual void OnBackPressed()
        {
            if (TryHandleBackInChildren()) return;
            Close();
        }

        /// <summary>Gives the back press to the active <see cref="IWindowBackHandler"/> children; true if one consumed it.</summary>
        protected bool TryHandleBackInChildren()
        {
            foreach (var handler in GetComponentsInChildren<IWindowBackHandler>())
            {
                if (handler is Behaviour { isActiveAndEnabled: false }) continue;
                if (handler.HandleBack()) return true;
            }

            return false;
        }

        /// <summary>Closes the window; <paramref name="result"/> is returned by <c>Open&lt;TWindow, TResult&gt;</c>.</summary>
        protected void Close(object result = null)
        {
            TryRequestClose(result);
        }

        /// <summary>
        /// Closes the window with <paramref name="result"/>. Returns false when the window is not open.
        /// </summary>
        public bool TryRequestClose(object result = null)
        {
            if (_requestClose == null) return false;

            _requestClose(result);
            return true;
        }

        /// <summary>Closes the window right away, without transition; Open returns the default result.</summary>
        public void ForceClose()
        {
            _forceClose?.Invoke();
        }

        /// <summary>Called on each opening, after <see cref="Options"/> is set and before <see cref="Setup"/>.</summary>
        protected virtual void OnOpening() { }

        internal void Bind(WindowOptions options, Action<object> requestClose, Action forceClose)
        {
            Options = options;
            _requestClose = requestClose;
            _forceClose = forceClose;
            OnOpening();
        }

        internal void Unbind()
        {
            _requestClose = null;
            _forceClose = null;
            Options = null;
        }

        /// <summary>Restores the rest state before an opening: hidden, not interactive, transition root in place.</summary>
        internal void PrepareForOpen()
        {
            EnsureComponents();
            Canvas.enabled = true; // May have been hidden under a full screen window in a previous opening.

            var root = TransitionUtils.GetTransitionRoot(this);
            if (root != _restRoot)
            {
                // First opening (or another root): its current state is the rest state.
                _restRoot = root;
                if (root)
                {
                    _restScale = root.localScale;
                    _restAnchoredPosition = root.anchoredPosition;
                }
            }
            else if (root)
            {
                root.localScale = _restScale;
                root.anchoredPosition = _restAnchoredPosition;
            }

            CanvasGroup.alpha = 0f;
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;

            foreach (var handler in GetComponentsInChildren<IWindowOpenHandler>(true))
            {
                handler.OnWindowOpening();
            }
        }
    }
}
