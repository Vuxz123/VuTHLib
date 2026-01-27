using _VuTH.Core.Window.Transition;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Base class for all UI views
    /// </summary>
    public abstract class UIViewBase : MonoBehaviour, IUIView
    {
        public GameObject GameObject => gameObject;
        public Canvas Canvas { get; private set; }
        public CanvasGroup CanvasGroup { get; private set; }
        
        protected UniTaskCompletionSource<object> CloseSource;
        
        protected bool IsShowing { get; private set; }
        
        protected virtual void Awake()
        {
            Canvas = GetComponent<Canvas>();
            CanvasGroup = GetComponent<CanvasGroup>();
            
            if (!Canvas) Canvas = gameObject.AddComponent<Canvas>();
            if (!CanvasGroup) CanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        public virtual void Setup(object data) { }
        
        public virtual async UniTask Show(IUITransition transition = null)
        {
            gameObject.SetActive(true);
            IsShowing = true;
            
            if (transition != null)
            {
                await transition.In(this);
            }
            else
            {
                CanvasGroup.alpha = 1f;
                CanvasGroup.interactable = true;
                CanvasGroup.blocksRaycasts = true;
            }
            
            OnShown();
        }
        
        public virtual async UniTask Hide(IUITransition transition = null)
        {
            IsShowing = false;
            CanvasGroup.interactable = false;
            
            if (transition != null)
            {
                await transition.Out(this);
            }
            else
            {
                CanvasGroup.alpha = 0f;
            }
            
            CanvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            
            OnHidden();
        }
        
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
        
        public void SetCloseSource(UniTaskCompletionSource<object> source)
        {
            // If we already have a close source and it's being replaced, complete it
            if (CloseSource != null && source != CloseSource)
            {
                CloseSource.TrySetResult(null);
            }
            CloseSource = source;
        }
        
        protected void Close(object result = null)
        {
            CloseSource?.TrySetResult(result);
        }
        
        /// <summary>
        /// Force close without waiting for user interaction
        /// </summary>
        public void ForceClose()
        {
            CloseSource?.TrySetResult(null);
        }

        /// <summary>
        /// Request this view to close. This is the reliable way for WindowManager to close a window.
        /// </summary>
        public bool TryRequestClose(object result = null)
        {
            if (CloseSource == null)
                return false;

            return CloseSource.TrySetResult(result);
        }
        
        public virtual void OnBackPressed()
        {
            Close(); // Default: close with null result
        }
        
        public virtual void OnViewShown() { }
        public virtual void OnViewHidden() { }
    }
}