using System;
using Common;
using Cysharp.Threading.Tasks;

namespace Core.Window
{
    /// <summary>
    /// Window/Popup management - Stackable overlays within a Screen
    /// </summary>
    public interface IWindowManager : ICommonManager
    {
        /// <summary>
        /// Open a window and wait for result. <br/>
        /// Note: a non-default result is returned only when the window is closed with a payload <br/>
        /// (e.g., view calls TryRequestClose(result)/Close(result) or manager calls Close/CloseTop(result)).
        /// </summary>
        UniTask<TResult> Open<TWindow, TResult>(object data = null) where TWindow : UIViewBase;
        
        /// <summary>
        /// Open a window with options and wait for result. <br/>
        /// Note: a non-default result is returned only when the window is closed with a payload <br/>
        /// (e.g., view calls TryRequestClose(result)/Close(result) or manager calls Close/CloseTop(result)).
        /// </summary>
        UniTask<TResult> Open<TWindow, TResult>(WindowOptions options) where TWindow : UIViewBase;
        
        /// <summary>
        /// Open a window without waiting for result
        /// </summary>
        UniTask Open<TWindow>(object data = null) where TWindow : UIViewBase;
        
        /// <summary>
        /// Close a specific window
        /// </summary>
        UniTask Close(UIViewBase popup);
        
        /// <summary>
        /// Close a specific window and provide a result payload for Open&lt;TWindow, TResult&gt;.
        /// </summary>
        UniTask Close(UIViewBase popup, object result);

        /// <summary>
        /// Close the topmost window
        /// </summary>
        UniTask CloseTop();
        
        /// <summary>
        /// Close the topmost window and provide a result payload for Open&lt;TWindow, TResult&gt;.
        /// </summary>
        UniTask CloseTop(object result);
        
        /// <summary>
        /// Close all windows
        /// </summary>
        UniTask CloseAll(bool immediate = false, bool forceCleanup = false);
        
        /// <summary>
        /// Get the topmost window
        /// </summary>
        UIViewBase TopWindow { get; }
        
        /// <summary>
        /// Number of windows in stack
        /// </summary>
        int WindowCount { get; }
        
        /// <summary>
        /// Whether a transition is in progress
        /// </summary>
        bool IsTransitioning { get; }
        
        /// <summary>
        /// Check if a specific window type is in the stack
        /// </summary>
        bool HasWindow<TWindow>() where TWindow : UIViewBase;
        
        /// <summary>
        /// Get a specific window from the stack
        /// </summary>
        TWindow GetWindow<TWindow>() where TWindow : UIViewBase;
        
        /// <summary>
        /// Release the cached Addressables prefab handle for a specific window type (if loaded).
        /// </summary>
        void ReleasePrefab<TWindow>() where TWindow : UIViewBase;

        /// <summary>
        /// Release all cached Addressables prefab handles.
        /// Useful for long sessions or when you want to unload UI bundles.
        /// </summary>
        void ClearPrefabCache();

        event Action<UIViewBase> OnWindowOpened;
        event Action<UIViewBase> OnWindowClosed;
    }
}