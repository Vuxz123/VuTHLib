using System;
using System.Threading;
using _VuTH.Common;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Window/Popup management - stackable overlays within a Screen.
    /// <para>
    /// <c>Open</c> completes when the window <b>closes</b> (with its result); <c>Show</c> completes once the window is
    /// <b>shown</b>. Operations run one after another, so opens and closes never interleave.
    /// </para>
    /// </summary>
    public interface IWindowManager : ICommonManager
    {
        /// <summary>
        /// Opens a window and completes when it closes, with the result it was closed with (view
        /// <c>Close(result)</c>, or <see cref="Close(UIViewBase, object)"/> / <see cref="CloseTop(object)"/>).
        /// Default when closed without a result, by back press, by <see cref="CloseAll"/>, or when it cannot be opened.
        /// Canceling <paramref name="cancellationToken"/> closes the window and throws <see cref="OperationCanceledException"/>.
        /// </summary>
        UniTask<TResult> Open<TWindow, TResult>(object data = null, CancellationToken cancellationToken = default)
            where TWindow : UIViewBase;

        /// <inheritdoc cref="Open{TWindow, TResult}(object, CancellationToken)"/>
        UniTask<TResult> Open<TWindow, TResult>(WindowOptions options, CancellationToken cancellationToken = default)
            where TWindow : UIViewBase;

        /// <summary>
        /// Opens a window and completes when it <b>closes</b>. To continue as soon as it is shown, use
        /// <see cref="Show{TWindow}(object)"/>.
        /// </summary>
        UniTask Open<TWindow>(object data = null, CancellationToken cancellationToken = default) where TWindow : UIViewBase;

        /// <summary>
        /// Opens a window and completes once it finished transitioning in, returning it (null if it could not be
        /// opened). The window stays open until closed.
        /// </summary>
        UniTask<TWindow> Show<TWindow>(object data = null) where TWindow : UIViewBase;

        /// <inheritdoc cref="Show{TWindow}(object)"/>
        UniTask<TWindow> Show<TWindow>(WindowOptions options) where TWindow : UIViewBase;

        /// <summary>Closes a window; completes once it is closed.</summary>
        UniTask Close(UIViewBase window);

        /// <summary>
        /// Closes a window with a result payload for Open&lt;TWindow, TResult&gt;; completes once it is closed.
        /// </summary>
        UniTask Close(UIViewBase window, object result);

        /// <summary>Closes the topmost window that is not an overlay (a toast is skipped); completes once it is closed.</summary>
        UniTask CloseTop();

        /// <summary>
        /// Closes the topmost window with a result payload for Open&lt;TWindow, TResult&gt;.
        /// </summary>
        UniTask CloseTop(object result);

        /// <summary>
        /// Closes all windows; Open calls return the default result. <paramref name="immediate"/> skips the out
        /// transitions; <paramref name="forceCleanup"/> also removes the windows at once, without waiting for
        /// operations in progress.
        /// </summary>
        UniTask CloseAll(bool immediate = false, bool forceCleanup = false);

        /// <summary>
        /// Closes the windows that do not keep open on a Screen change (<see cref="WindowOptions.KeepOnScreenChange"/>).
        /// Called by <c>CloseWindowsOnScreenChange</c> (Screen integration), or by your own screen flow.
        /// </summary>
        UniTask CloseForScreenChangeAsync(bool immediate = true);

        /// <summary>
        /// Shows a toast and closes it after <paramref name="duration"/> seconds (unscaled; default: the toast's
        /// <see cref="ToastBase.Duration"/>). Completes when it is closed.
        /// </summary>
        UniTask ShowToast<TToast>(object data = null, float? duration = null) where TToast : ToastBase;

        /// <summary>
        /// Sends a back press to the top window (its <see cref="UIViewBase.OnBackPressed"/>, which closes it by
        /// default) unless it opted out. Returns whether a window received it, so the caller can handle it otherwise.
        /// The manager calls it for its back action and Escape.
        /// </summary>
        UniTask<bool> HandleBackPressAsync();

        /// <summary>The topmost window that is not an overlay: a toast on top is never the top window.</summary>
        UIViewBase TopWindow { get; }

        /// <summary>Number of open windows, overlays (toasts) included.</summary>
        int WindowCount { get; }

        /// <summary>Modal windows open, whether the Screen is covered by a full screen window, top window.</summary>
        WindowStackState StackState { get; }

        /// <summary>
        /// Raised when <see cref="StackState"/> changes: pause gameplay while <see cref="WindowStackState.HasModal"/>,
        /// stop rendering the Screen while <see cref="WindowStackState.IsScreenCovered"/>.
        /// </summary>
        event Action<WindowStackState> OnStackChanged;

        /// <summary>
        /// Raised when the manager's own back input (back action / Escape) reaches no window, e.g. to show a "quit the
        /// game?" dialog. Not raised for <see cref="HandleBackPressAsync"/> calls made by the game.
        /// </summary>
        event Action OnBackPressUnhandled;

        /// <summary>
        /// Whether a transition is in progress
        /// </summary>
        bool IsTransitioning { get; }

        /// <summary>Whether a window of type <typeparamref name="TWindow"/> (or derived) is open and not closing.</summary>
        bool HasWindow<TWindow>() where TWindow : UIViewBase;

        /// <summary>The topmost window of type <typeparamref name="TWindow"/> (or derived) that is not closing, or null.</summary>
        TWindow GetWindow<TWindow>() where TWindow : UIViewBase;

        /// <summary>
        /// Queues a window: it opens when no window is open (nor opening), highest <paramref name="priority"/> first,
        /// then in call order. Use it for windows that must not pile up, such as rewards, offers and rating prompts.
        /// Completes when the window closes; canceling removes it from the queue (or closes it if already open).
        /// With <see cref="WindowDuplicatePolicy.Ignore"/>, a window type already queued is not queued twice.
        /// </summary>
        UniTask<TResult> Enqueue<TWindow, TResult>(WindowOptions options, int priority = 0,
            CancellationToken cancellationToken = default) where TWindow : UIViewBase;

        /// <inheritdoc cref="Enqueue{TWindow, TResult}"/>
        UniTask Enqueue<TWindow>(object data = null, int priority = 0, CancellationToken cancellationToken = default)
            where TWindow : UIViewBase;

        /// <summary>Number of queued windows not opened yet.</summary>
        int QueuedCount { get; }

        /// <summary>Removes every queued window not opened yet; their Enqueue calls are canceled.</summary>
        void ClearQueue();

        /// <summary>
        /// Loads the prefab of <typeparamref name="TWindow"/> and creates <paramref name="instances"/> pooled
        /// instances, so the first opening does not hitch.
        /// </summary>
        UniTask PreloadAsync<TWindow>(int instances = 1) where TWindow : UIViewBase;

        /// <summary>Snapshot of the stack, bottom to top. For debugging tools.</summary>
        System.Collections.Generic.IReadOnlyList<WindowInfo> GetOpenWindows();

        /// <summary>
        /// Releases the prefab of a window type together with its pooled and kept instances (they reference its assets).
        /// Skipped (with a warning) while a window of that type is open.
        /// </summary>
        void ReleasePrefab<TWindow>() where TWindow : UIViewBase;

        /// <summary>
        /// <see cref="ReleasePrefab{TWindow}"/> for every loaded window type that is not open. Useful for long sessions
        /// or to unload UI bundles.
        /// </summary>
        void ClearPrefabCache();

        event Action<UIViewBase> OnWindowOpened;
        event Action<UIViewBase> OnWindowClosed;

        /// <summary>
        /// Opened / closed with details (close cause, time shown, result). Also published through MessagePipe as
        /// <see cref="WindowEvent"/> when the manager is created through DI.
        /// </summary>
        event Action<WindowEvent> OnWindowEvent;
    }
}
