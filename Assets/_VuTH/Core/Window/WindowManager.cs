using System;
using System.Collections.Generic;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Pool;
using _VuTH.Core.Window.Blocker;
using _VuTH.Core.Window.Profile;
using _VuTH.Core.Window.Transition;
using Cysharp.Threading.Tasks;
using MessagePipe;
using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;

#if VCONTAINER
using VContainer;
#endif

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Opens windows (prefabs loaded through <see cref="IWindowPrefabProvider"/>, instances from the pool), stacks them
    /// and closes them with their transitions.
    /// <para>
    /// Opens and closes run one after another. UI input is blocked only while a window transitions in or out, never
    /// while it is simply open. A window that fails to open, throws, or is destroyed is always removed from the stack,
    /// and whoever awaits it is released.
    /// </para>
    /// </summary>
    public class WindowManager : VBootstrapManager<WindowManager, IWindowManager>, IWindowManager
    {
        private const string TransitionBlockReason = "WindowTransition";

        [Header("Config")]
        [SerializeField, ReadOnlyField] private WindowProfile config;
        [SerializeField] private Transform windowRoot;

        [SerializeField] private Component inputBlockerComponent;
        [SerializeField] private Component transitionFactoryComponent;

        [Header("Input")]
        [Tooltip("Optional. If set, this InputAction will be used as the Back button (recommended for mobile).")]
        [SerializeField] private InputActionReference backAction;

        // Injected; falls back to the PoolManager singleton without DI, and to Instantiate / Destroy without a pool.
        private IPoolManager _pool;
        private IPublisher<WindowEvent> _eventPublisher;

        // Closed windows kept for their next opening (KeepInstance), one per window type.
        private readonly Dictionary<Type, (UIViewBase Window, bool Pooled)> _keptInstances = new();

        private IUIInputBlocker _inputBlocker;
        private IUITransitionFactory _transitionFactory;
        private IWindowPrefabProvider _prefabProvider;
        private readonly IUITransitionRunner _transitionRunner = new UITransitionRunner();

        // Open windows (index 0 = bottom, last = top) + resolved options per window.
        private readonly WindowStack<UIViewBase> _windows = new();
        private readonly Dictionary<UIViewBase, WindowEntry> _entries = new();

        private readonly SemaphoreSlim _opGate = new(1, 1);
        private int _transitionCount;
        private int _pendingOpens;
        private bool _backPressedThisFrame;

        // Queued opens (highest priority first, then oldest first); one runs at a time, when no window is open.
        private readonly List<QueuedOpen> _queue = new();
        private long _queueSequence;
        private bool _queueBusy;

        public UIViewBase TopWindow => _windows.Top;
        public int WindowCount => _windows.Count;
        public bool IsTransitioning => _transitionCount > 0;
        public int QueuedCount => _queue.Count;

        /// <summary>The input blocker in use (null when blocking is disabled). For debugging tools.</summary>
        public IUIInputBlocker InputBlocker => _inputBlocker;

        public event Action<UIViewBase> OnWindowOpened;
        public event Action<UIViewBase> OnWindowClosed;
        public event Action<WindowEvent> OnWindowEvent;

        private IWindowPrefabProvider PrefabProvider => _prefabProvider ??= new WindowPrefabCache(this);

        private enum CloseReason
        {
            Normal,
            CloseAll,
            Force,
            Canceled,
            Replaced,
        }

        private readonly struct CloseToken
        {
            public readonly CloseReason Reason;
            public readonly object Payload;

            public CloseToken(CloseReason reason, object payload = null)
            {
                Reason = reason;
                Payload = payload;
            }
        }

        /// <summary>One opening of one window.</summary>
        private sealed class WindowEntry
        {
            public readonly UIViewBase Window;
            public readonly WindowOptions Options;
            public readonly bool Pooled;
            public readonly UniTaskCompletionSource<CloseToken> CloseSource = new();
            public readonly UniTaskCompletionSource Closed = new();
            public WindowState State = WindowState.Opening;
            public bool Shown;
            public bool SkipOutTransition;
            public bool Cleaned;
            public float ShownAt;
            public bool BackPressed;
            public CloseReason Reason = CloseReason.Force;
            public bool HasResult;

            public WindowEntry(UIViewBase window, WindowOptions options, bool pooled)
            {
                Window = window;
                Options = options;
                Pooled = pooled;
            }

            public bool CloseRequested => CloseSource.Task.Status != UniTaskStatus.Pending;

            public bool RequestClose(CloseReason reason, object payload = null)
            {
                if (CloseRequested) return false;

                // Recorded before completing: the continuation may run synchronously.
                Reason = reason;
                HasResult = payload != null;
                return CloseSource.TrySetResult(new CloseToken(reason, payload));
            }
        }

        /// <summary>The window an open call got: a new one it owns, or an open one it shares (duplicate policy).</summary>
        private readonly struct OpenTicket
        {
            public readonly WindowEntry Entry;
            public readonly bool Owned;

            public OpenTicket(WindowEntry entry, bool owned)
            {
                Entry = entry;
                Owned = owned;
            }
        }

        private sealed class QueuedOpen
        {
            public int Priority;
            public long Sequence;
            public Type WindowType;
            public object Source; // UniTaskCompletionSource<TResult>
            public Func<UniTask> Start;
            public Action Cancel;
        }

#if VCONTAINER
        /// <summary>Stores dependencies only; they are used from <see cref="InitializeBootstrap"/> on.</summary>
        [Inject]
        public void Construct(IPoolManager pool, IPublisher<WindowEvent> eventPublisher)
        {
            _pool = pool;
            _eventPublisher = eventPublisher;
        }
#endif

        #region Configuration

        /// <summary>Uses <paramref name="provider"/> to load window prefabs instead of Addressables.</summary>
        public WindowManager WithPrefabProvider(IWindowPrefabProvider provider)
        {
            _prefabProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            return this;
        }

        /// <summary>Uses <paramref name="blocker"/> instead of the serialized input blocker (null: no blocking).</summary>
        public WindowManager WithInputBlocker(IUIInputBlocker blocker)
        {
            _inputBlocker = blocker;
            return this;
        }

        /// <summary>Uses <paramref name="factory"/> instead of the serialized one (null: no transitions).</summary>
        public WindowManager WithTransitionFactory(IUITransitionFactory factory)
        {
            _transitionFactory = factory;
            return this;
        }

        #endregion

        #region Lifecycle

        protected override void InitializeBootstrap()
        {
            EnsureProfile();

            if (_inputBlocker == null && inputBlockerComponent)
                _inputBlocker = inputBlockerComponent.GetComponent<IUIInputBlocker>();

            if (_transitionFactory == null && transitionFactoryComponent)
                _transitionFactory = transitionFactoryComponent.GetComponent<IUITransitionFactory>();

            if (!_pool.IsAlive()) _pool = PoolManager.HasInstance ? PoolManager.Instance : null;

            if (_pool == null)
                this.LogWarning("No PoolManager: windows are instantiated and destroyed instead of pooled.");

            if (_inputBlocker == null)
                this.LogWarning("No IUIInputBlocker assigned. Input blocking will be disabled.");

            if (_transitionFactory == null)
                this.LogWarning("No IUITransitionFactory assigned. Transitions will be disabled.");

            if (backAction != null && backAction.action != null)
            {
                backAction.action.Enable();
                backAction.action.performed += OnBackActionPerformed;
            }
        }

        protected override void DeinitializeBootstrap()
        {
            if (backAction != null && backAction.action != null)
            {
                backAction.action.performed -= OnBackActionPerformed;
                backAction.action.Disable();
            }

            ClearQueue();

            // Release every window and whoever awaits it: nothing will close them any more.
            foreach (var entry in new List<WindowEntry>(_entries.Values))
            {
                entry.RequestClose(CloseReason.CloseAll);
                Cleanup(entry);
            }

            _windows.Clear();
            DestroyKeptInstances();
            ClearPrefabCache();
        }

        private void EnsureProfile()
        {
            if (config != null) return;
            if (WindowProfileUtilities.TryGetProfile(out var profile))
            {
                config = profile;
                return;
            }

            this.LogError($"No WindowProfile in Resources/{WindowManagerConst.WindowProfilePath}: using the defaults.");
            config = ScriptableObject.CreateInstance<WindowProfile>();
        }

        public void ReleasePrefab<TWindow>() where TWindow : UIViewBase
        {
            PrefabProvider.Release(typeof(TWindow));
        }

        public void ClearPrefabCache()
        {
            DestroyKeptInstances();
            PrefabProvider.Clear();
        }

        /// <summary>Releases the closed instances kept by <see cref="WindowOptions.KeepInstance"/>.</summary>
        public void DestroyKeptInstances()
        {
            foreach (var kept in _keptInstances.Values)
            {
                if (kept.Window) Release(kept.Window.gameObject, kept.Pooled);
            }

            _keptInstances.Clear();
        }

        public async UniTask PreloadAsync<TWindow>(int instances = 1) where TWindow : UIViewBase
        {
            var prefab = await PrefabProvider.GetOrLoad(typeof(TWindow));
            if (!prefab || instances <= 0 || !_pool.IsAlive()) return;

            // WarmupPool replaces an existing pool (losing track of its instances): only warm up a new one.
            if (_pool.GetPrefabStats(prefab) != null) return;
            _pool.WarmupPool(prefab, instances);
        }

        #endregion

        #region Open

        public UniTask<TResult> Open<TWindow, TResult>(object data = null, CancellationToken cancellationToken = default)
            where TWindow : UIViewBase
        {
            return Open<TWindow, TResult>(new WindowOptions { Data = data }, cancellationToken);
        }

        public UniTask Open<TWindow>(object data = null, CancellationToken cancellationToken = default)
            where TWindow : UIViewBase
        {
            return Open<TWindow, object>(new WindowOptions { Data = data }, cancellationToken);
        }

        public async UniTask<TResult> Open<TWindow, TResult>(WindowOptions options, CancellationToken cancellationToken = default)
            where TWindow : UIViewBase
        {
            var ticket = await BeginOpenAsync<TWindow>(options, cancellationToken);
            var entry = ticket.Entry;
            if (entry == null) return default;

            CloseToken token;
            if (ticket.Owned)
            {
                token = await RunUntilClosedAsync(entry, cancellationToken);
            }
            else
            {
                // A shared window belongs to its first caller: canceling here only stops waiting for it.
                token = cancellationToken.CanBeCanceled
                    ? await entry.CloseSource.Task.AttachExternalCancellation(cancellationToken)
                    : await entry.CloseSource.Task;
                await entry.Closed.Task;
            }

            if (token.Reason == CloseReason.Canceled)
            {
                if (ticket.Owned) throw new OperationCanceledException(cancellationToken);
                return default; // The owner's call was canceled, not this one.
            }

            if (token.Reason != CloseReason.Normal || token.Payload == null) return default;
            if (token.Payload is TResult result) return result;

            this.LogError($"{typeof(TWindow).Name} was closed with a {token.Payload.GetType().Name} result, but " +
                          $"{typeof(TResult).Name} was expected; returning the default.");
            return default;
        }

        public UniTask<TWindow> Show<TWindow>(object data = null) where TWindow : UIViewBase
        {
            return Show<TWindow>(new WindowOptions { Data = data });
        }

        public async UniTask<TWindow> Show<TWindow>(WindowOptions options) where TWindow : UIViewBase
        {
            var ticket = await BeginOpenAsync<TWindow>(options, CancellationToken.None);
            var entry = ticket.Entry;
            if (entry == null) return null;

            if (ticket.Owned)
            {
                RunUntilClosedAsync(entry, CancellationToken.None)
                    .Forget(e => this.LogError($"Closing {typeof(TWindow).Name} failed: {e}"));
            }

            return entry.Cleaned ? null : (TWindow)entry.Window;
        }

        /// <summary>Loads the prefab and opens the window (or shares an open one). No entry if it could not be opened.</summary>
        private async UniTask<OpenTicket> BeginOpenAsync<TWindow>(WindowOptions options, CancellationToken cancellationToken)
            where TWindow : UIViewBase
        {
            cancellationToken.ThrowIfCancellationRequested();

            _pendingOpens++;
            try
            {
                var prefab = await PrefabProvider.GetOrLoad(typeof(TWindow));
                if (!prefab) return default;

                cancellationToken.ThrowIfCancellationRequested();
                return await RunSerialized(() => OpenEntryAsync<TWindow>(prefab, options));
            }
            finally
            {
                _pendingOpens--;
                PumpQueue();
            }
        }

        private async UniTask<OpenTicket> OpenEntryAsync<TWindow>(GameObject prefab, WindowOptions options)
            where TWindow : UIViewBase
        {
            // Resolved from the prefab: the duplicate policy is needed before spawning.
            var resolved = WindowOptionsResolver.Resolve(options, prefab.GetComponent<TWindow>() as IWindowDefinition);

            var existing = FindOpen(typeof(TWindow));
            if (existing != null)
            {
                switch (resolved.Duplicate.GetValueOrDefault())
                {
                    case WindowDuplicatePolicy.Ignore:
                        return new OpenTicket(existing, false);

                    case WindowDuplicatePolicy.BringToFront:
                        BringToFront(existing);
                        return new OpenTicket(existing, false);

                    case WindowDuplicatePolicy.ReplaceExisting:
                        existing.RequestClose(CloseReason.Replaced);
                        await CloseEntryAsync(existing, new CloseToken(CloseReason.Replaced));
                        break;
                }
            }

            var window = SpawnWindow<TWindow>(prefab, out var pooled);
            if (!window) return default;

            var entry = new WindowEntry(window, resolved, pooled);
            _entries[window] = entry;
            _windows.Push(window);
            _windows.SetOptions(window, resolved);

            try
            {
                window.PrepareForOpen();
                window.Canvas.overrideSorting = true;
                window.Canvas.sortingOrder = GetSortingOrder(resolved.WindowType.GetValueOrDefault(), _windows.Count);
                window.transform.SetAsLastSibling();

                window.Bind(
                    result => entry.RequestClose(CloseReason.Normal, result),
                    () => entry.RequestClose(CloseReason.Force));
                WatchLifetime(entry);

                window.Setup(resolved.Data);

                await RunTransitionAsync(entry, isIn: true);
            }
            catch
            {
                // Never leave a half-opened window in the stack.
                entry.RequestClose(CloseReason.Force);
                Cleanup(entry);
                throw;
            }

            if (entry.Cleaned) return new OpenTicket(entry, true); // Removed meanwhile (forced CloseAll, destroyed).

            entry.Shown = true;
            entry.State = WindowState.Open;
            entry.ShownAt = Time.realtimeSinceStartup;
            Notify(OnWindowOpened, window);
            Publish(entry, WindowEventType.Opened);
            SafeCall(window.OnViewShown, window, nameof(UIViewBase.OnViewShown));
            return new OpenTicket(entry, true);
        }

        /// <summary>The topmost open window of exactly <paramref name="windowType"/> that is not closing.</summary>
        private WindowEntry FindOpen(Type windowType)
        {
            var windows = _windows.ToArray();
            for (var i = windows.Length - 1; i >= 0; i--)
            {
                if (windows[i] && windows[i].GetType() == windowType &&
                    _entries.TryGetValue(windows[i], out var entry) && !entry.Cleaned && !entry.CloseRequested)
                {
                    return entry;
                }
            }

            return null;
        }

        private void BringToFront(WindowEntry entry)
        {
            if (_windows.Top == entry.Window) return;

            _windows.Remove(entry.Window);
            _windows.Push(entry.Window);
            entry.Window.transform.SetAsLastSibling();
            RefreshSortingOrders();
        }

        /// <summary>Sorting orders from the current stack positions.</summary>
        private void RefreshSortingOrders()
        {
            var windows = _windows.ToArray();
            for (var i = 0; i < windows.Length; i++)
            {
                if (!windows[i] || !_entries.TryGetValue(windows[i], out var entry)) continue;
                windows[i].Canvas.sortingOrder = GetSortingOrder(entry.Options.WindowType.GetValueOrDefault(), i + 1);
            }
        }

        private TWindow SpawnWindow<TWindow>(GameObject prefab, out bool pooled) where TWindow : UIViewBase
        {
            if (_keptInstances.Remove(typeof(TWindow), out var kept) && kept.Window)
            {
                pooled = kept.Pooled;
                kept.Window.gameObject.SetActive(true);
                return (TWindow)kept.Window;
            }

            pooled = _pool.IsAlive();
            GameObject instance;
            if (pooled)
            {
                var spawned = _pool.Spawn(prefab.transform, parent: windowRoot);
                instance = spawned ? spawned.gameObject : null;
            }
            else
            {
                instance = Instantiate(prefab, windowRoot);
            }

            if (!instance)
            {
                this.LogError($"Could not spawn window prefab '{prefab.name}'.");
                return null;
            }

            var window = instance.GetComponent<TWindow>();
            if (!window)
            {
                this.LogError($"Spawned prefab '{prefab.name}' does not have component {typeof(TWindow).Name}");
                Release(instance, pooled);
                return null;
            }

            instance.SetActive(true);
            return window;
        }

        private void WatchLifetime(WindowEntry entry)
        {
            var go = entry.Window.gameObject;
            if (!go.TryGetComponent<WindowLifetimeWatcher>(out var watcher))
            {
                watcher = go.AddComponent<WindowLifetimeWatcher>();
                watcher.hideFlags = HideFlags.HideInInspector;
            }

            // Destroyed while open (scene unload, Destroy by game code): close without transition.
            watcher.Destroyed = () => entry.RequestClose(CloseReason.Force);
        }

        #endregion

        #region Queue

        public UniTask Enqueue<TWindow>(object data = null, int priority = 0, CancellationToken cancellationToken = default)
            where TWindow : UIViewBase
        {
            return Enqueue<TWindow, object>(new WindowOptions { Data = data }, priority, cancellationToken);
        }

        public UniTask<TResult> Enqueue<TWindow, TResult>(WindowOptions options, int priority = 0,
            CancellationToken cancellationToken = default) where TWindow : UIViewBase
        {
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled<TResult>(cancellationToken);

            // Ignore: one queued window of a type is enough.
            if (options?.Duplicate == WindowDuplicatePolicy.Ignore)
            {
                foreach (var queued in _queue)
                {
                    if (queued.WindowType == typeof(TWindow) && queued.Source is UniTaskCompletionSource<TResult> same)
                        return same.Task;
                }
            }

            var source = new UniTaskCompletionSource<TResult>();
            var item = new QueuedOpen
            {
                Priority = priority,
                Sequence = _queueSequence++,
                WindowType = typeof(TWindow),
                Source = source,
            };

            var registration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(() =>
                {
                    if (_queue.Remove(item)) source.TrySetCanceled(cancellationToken);
                })
                : default;

            item.Cancel = () =>
            {
                registration.Dispose();
                source.TrySetCanceled();
            };
            item.Start = async () =>
            {
                try
                {
                    source.TrySetResult(await Open<TWindow, TResult>(options, cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    source.TrySetCanceled(cancellationToken);
                }
                catch (Exception e)
                {
                    source.TrySetException(e);
                }
                finally
                {
                    registration.Dispose();
                }
            };

            var index = _queue.FindIndex(q => q.Priority < priority);
            _queue.Insert(index < 0 ? _queue.Count : index, item);

            PumpQueue();
            return source.Task;
        }

        public void ClearQueue()
        {
            var queued = new List<QueuedOpen>(_queue);
            _queue.Clear();
            foreach (var item in queued)
            {
                item.Cancel();
            }
        }

        /// <summary>Opens the next queued window when nothing else is open or opening.</summary>
        private void PumpQueue()
        {
            if (_queueBusy || _queue.Count == 0 || HasModalWindow() || _pendingOpens > 0 || !IsManagerInitialized)
                return;

            var item = _queue[0];
            _queue.RemoveAt(0);
            _queueBusy = true;
            RunQueuedAsync(item).Forget();
        }

        private async UniTaskVoid RunQueuedAsync(QueuedOpen item)
        {
            try
            {
                await item.Start();
            }
            finally
            {
                _queueBusy = false;
                PumpQueue();
            }
        }

        /// <summary>Whether a Popup / FullScreenPopup is open: System (toast, loading) and Tutorial windows do not count.</summary>
        private bool HasModalWindow()
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Cleaned) continue;
                var type = entry.Options.WindowType.GetValueOrDefault();
                if (type is WindowType.Popup or WindowType.FullScreenPopup) return true;
            }

            return false;
        }

        #endregion

        #region Toast

        public async UniTask ShowToast<TToast>(object data = null, float? duration = null) where TToast : ToastBase
        {
            var ticket = await BeginOpenAsync<TToast>(new WindowOptions { Data = data }, CancellationToken.None);
            var entry = ticket.Entry;
            if (entry == null || entry.Cleaned) return;

            var seconds = duration ?? ((ToastBase)entry.Window).Duration;
            CloseAfterAsync(entry, seconds).Forget();
            await RunUntilClosedAsync(entry, CancellationToken.None);
        }

        /// <summary>Closes this opening of the toast after <paramref name="seconds"/>, unless it is closed before.</summary>
        private static async UniTaskVoid CloseAfterAsync(WindowEntry entry, float seconds)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, seconds)), DelayType.UnscaledDeltaTime);
            entry.RequestClose(CloseReason.Normal);
        }

        #endregion

        #region Close

        /// <summary>Waits for the close request, then closes the window (unless it was already removed).</summary>
        private async UniTask<CloseToken> RunUntilClosedAsync(WindowEntry entry, CancellationToken cancellationToken)
        {
            var registration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(() => entry.RequestClose(CloseReason.Canceled))
                : default;

            CloseToken token;
            try
            {
                token = await entry.CloseSource.Task;
            }
            finally
            {
                registration.Dispose();
            }

            if (!entry.Cleaned)
            {
                await RunSerialized(() => CloseEntryAsync(entry, token));
            }

            return token;
        }

        /// <summary>Plays the out transition and removes the window. Must run inside the operation gate.</summary>
        private async UniTask CloseEntryAsync(WindowEntry entry, CloseToken token)
        {
            if (entry.Cleaned) return;
            entry.State = WindowState.Closing;

            try
            {
                var skipOut = entry.SkipOutTransition || token.Reason == CloseReason.Force || !entry.Window;
                if (!skipOut) await RunTransitionAsync(entry, isIn: false);
            }
            finally
            {
                Cleanup(entry);
            }
        }

        /// <summary>
        /// Removes the window from the stack and returns it to the pool. Idempotent; never throws.
        /// </summary>
        private void Cleanup(WindowEntry entry)
        {
            if (entry.Cleaned) return;
            entry.Cleaned = true;

            var window = entry.Window;
            _windows.Remove(window);
            _windows.RemoveOptions(window);
            _entries.Remove(window);

            if (window)
            {
                window.Unbind();
                if (window.TryGetComponent<WindowLifetimeWatcher>(out var watcher)) watcher.Destroyed = null;

                // A transition still running would keep animating the pooled instance.
                Tween.StopAll(window.CanvasGroup);
                var root = TransitionUtils.GetTransitionRoot(window);
                if (root) Tween.StopAll(root);
            }

            if (entry.Shown)
            {
                Notify(OnWindowClosed, window);
                if (window) SafeCall(window.OnViewHidden, window, nameof(UIViewBase.OnViewHidden));
                Publish(entry, WindowEventType.Closed);
            }

            if (window)
            {
                if (entry.Options.KeepInstance.GetValueOrDefault() && !_keptInstances.ContainsKey(window.GetType()))
                {
                    window.gameObject.SetActive(false);
                    _keptInstances[window.GetType()] = (window, entry.Pooled);
                }
                else
                {
                    Release(window.gameObject, entry.Pooled);
                }
            }

            entry.Closed.TrySetResult();

            PumpQueue();
        }

        private void Release(GameObject instance, bool pooled)
        {
            try
            {
                if (pooled && _pool.IsAlive()) _pool.Despawn(instance);
                else Destroy(instance);
            }
            catch (Exception e)
            {
                this.LogError($"Could not release window '{instance.name}': {e}");
            }
        }

        public UniTask Close(UIViewBase window)
        {
            return Close(window, null);
        }

        public UniTask Close(UIViewBase window, object result)
        {
            if (window == null || !_entries.TryGetValue(window, out var entry))
            {
                this.LogWarning($"Close: {(window ? window.name : "null")} is not an open window.");
                return UniTask.CompletedTask;
            }

            entry.RequestClose(CloseReason.Normal, result);
            return entry.Closed.Task;
        }

        public UniTask CloseTop()
        {
            return CloseTop(null);
        }

        public UniTask CloseTop(object result)
        {
            var top = _windows.Top;
            return top ? Close(top, result) : UniTask.CompletedTask;
        }

        public UniTask CloseAll(bool immediate = false, bool forceCleanup = false)
        {
            return CloseEntriesAsync(TopFirstEntries(_ => true), immediate, forceCleanup);
        }

        public UniTask CloseForScreenChangeAsync(bool immediate = true)
        {
            return CloseEntriesAsync(TopFirstEntries(e => !e.Options.KeepOnScreenChange.GetValueOrDefault()), immediate, false);
        }

        private List<WindowEntry> TopFirstEntries(Func<WindowEntry, bool> filter)
        {
            var entries = new List<WindowEntry>();
            var windows = _windows.ToArray();
            for (var i = windows.Length - 1; i >= 0; i--)
            {
                if (_entries.TryGetValue(windows[i], out var entry) && filter(entry)) entries.Add(entry);
            }

            return entries;
        }

        private async UniTask CloseEntriesAsync(List<WindowEntry> entries, bool immediate, bool forceCleanup)
        {
            if (forceCleanup)
            {
                // Removed now, without waiting for operations in progress; their continuations see Cleaned.
                foreach (var entry in entries)
                {
                    entry.RequestClose(CloseReason.CloseAll);
                    Cleanup(entry);
                }

                return;
            }

            var closed = new List<UniTask>(entries.Count);
            foreach (var entry in entries)
            {
                entry.SkipOutTransition |= immediate;
                entry.RequestClose(CloseReason.CloseAll);
                closed.Add(entry.Closed.Task);
            }

            await UniTask.WhenAll(closed);
        }

        #endregion

        #region Query Methods

        public bool HasWindow<TWindow>() where TWindow : UIViewBase
        {
            return _windows.HasWindowOfType<TWindow>();
        }

        public TWindow GetWindow<TWindow>() where TWindow : UIViewBase
        {
            return _windows.GetWindowOfType<TWindow>();
        }

        public IReadOnlyList<WindowInfo> GetOpenWindows()
        {
            var windows = _windows.ToArray();
            var result = new List<WindowInfo>(windows.Length);
            foreach (var window in windows)
            {
                if (!_entries.TryGetValue(window, out var entry)) continue;

                result.Add(new WindowInfo(window, entry.Options.WindowType.GetValueOrDefault(),
                    window ? window.Canvas.sortingOrder : 0, entry.State,
                    entry.Options.CloseOnBackPress.GetValueOrDefault(true)));
            }

            return result;
        }

        #endregion

        #region Helpers

        private async UniTask<T> RunSerialized<T>(Func<UniTask<T>> op)
        {
            await _opGate.WaitAsync();
            try
            {
                return await op();
            }
            finally
            {
                _opGate.Release();
            }
        }

        private async UniTask RunSerialized(Func<UniTask> op)
        {
            await _opGate.WaitAsync();
            try
            {
                await op();
            }
            finally
            {
                _opGate.Release();
            }
        }

        /// <summary>
        /// Runs the in / out transition, blocking UI input only for its duration. A failing transition is logged and
        /// the final state applied, so it cannot leave the window half shown.
        /// </summary>
        private async UniTask RunTransitionAsync(WindowEntry entry, bool isIn)
        {
            var window = entry.Window;
            if (!window) return;

            var options = entry.Options;
            var block = _inputBlocker != null && config.blockInputDuringTransitions && options.BlockInput.GetValueOrDefault(true);

            _transitionCount++;
            if (block) _inputBlocker.Block(TransitionBlockReason);
            try
            {
                var transition = CreateTransition(options, isIn);
                try
                {
                    if (isIn) await _transitionRunner.RunIn(window, transition);
                    else await _transitionRunner.RunOut(window, transition);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    if (!window) return; // Destroyed meanwhile: nothing left to animate.
                    this.LogError($"{(isIn ? "In" : "Out")} transition of {window.name} failed: {e}");
                    if (isIn) await _transitionRunner.RunIn(window, null);
                    else await _transitionRunner.RunOut(window, null);
                }
            }
            finally
            {
                if (block) _inputBlocker.Unblock(TransitionBlockReason);
                _transitionCount--;
            }
        }

        private IUITransition CreateTransition(WindowOptions options, bool isIn)
        {
            if (_transitionFactory == null) return null;

            var settings = isIn ? options.TransitionInSettings : options.TransitionOutSettings;
            return settings != null
                ? _transitionFactory.Create(settings)
                : _transitionFactory.Create(options.TransitionPreset);
        }

        private int GetSortingOrder(WindowType windowType, int stackCountAfterPush)
        {
            return WindowSortingOrder.Compute(windowType, stackCountAfterPush,
                config.popupBaseSortingOrder, config.systemBaseSortingOrder, config.sortingOrderStep);
        }

        private void Notify(Action<UIViewBase> handler, UIViewBase window)
        {
            if (handler == null) return;

            try
            {
                handler(window);
            }
            catch (Exception e)
            {
                // A subscriber must not break the window flow.
                this.LogError($"A window event subscriber threw: {e}");
            }
        }

        private void Publish(WindowEntry entry, WindowEventType type)
        {
            if (OnWindowEvent == null && _eventPublisher == null) return;

            var window = entry.Window;
            var evt = new WindowEvent
            {
                Type = type,
                WindowName = window ? window.GetType().Name : "(destroyed)",
                WindowType = entry.Options.WindowType.GetValueOrDefault(),
                Window = window ? window : null,
            };

            if (type == WindowEventType.Closed)
            {
                evt.ShownSeconds = Time.realtimeSinceStartup - entry.ShownAt;
                evt.HasResult = entry.HasResult;
                evt.CloseCause = entry.Reason switch
                {
                    CloseReason.Normal => entry.BackPressed ? WindowCloseCause.BackPress : WindowCloseCause.Closed,
                    CloseReason.CloseAll => WindowCloseCause.CloseAll,
                    CloseReason.Canceled => WindowCloseCause.Canceled,
                    CloseReason.Replaced => WindowCloseCause.Replaced,
                    _ => WindowCloseCause.Forced,
                };
            }

            if (OnWindowEvent != null)
            {
                try
                {
                    OnWindowEvent(evt);
                }
                catch (Exception e)
                {
                    this.LogError($"A window event subscriber threw: {e}");
                }
            }

            if (_eventPublisher != null)
            {
                try
                {
                    _eventPublisher.Publish(evt);
                }
                catch (Exception e)
                {
                    this.LogError($"A WindowEvent subscriber threw: {e}");
                }
            }
        }

        private void SafeCall(Action call, UIViewBase window, string name)
        {
            try
            {
                call();
            }
            catch (Exception e)
            {
                this.LogError($"{name} of {(window ? window.name : "a destroyed window")} threw: {e}");
            }
        }

        #endregion

        #region Back Input

        private void OnBackActionPerformed(InputAction.CallbackContext ctx)
        {
            // Consumed in Update (main thread).
            _backPressedThisFrame = true;
        }

        private void Update()
        {
            var backPressed = _backPressedThisFrame
                              || (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame);
            _backPressedThisFrame = false;
            if (!backPressed) return;

            HandleBackPressAsync().Forget(ex => this.LogError($"Back press handling failed: {ex}"));
        }

        public UniTask<bool> HandleBackPressAsync()
        {
            // Serialized: a back press never closes a window that is still transitioning in.
            return RunSerialized(() =>
            {
                // The topmost window that is not a System overlay (toast, loading): those never take the back press.
                var target = TopFirstEntries(e => e.Options.WindowType.GetValueOrDefault() != WindowType.System);
                if (target.Count == 0) return UniTask.FromResult(false);

                var entry = target[0];
                var window = entry.Window;
                if (!window || !entry.Shown || entry.CloseRequested) return UniTask.FromResult(false);
                if (!entry.Options.CloseOnBackPress.GetValueOrDefault(true)) return UniTask.FromResult(false);

                entry.BackPressed = true;
                SafeCall(window.OnBackPressed, window, nameof(UIViewBase.OnBackPressed));
                if (!entry.CloseRequested) entry.BackPressed = false; // Consumed inside the window (e.g. a tab).
                return UniTask.FromResult(true);
            });
        }

        #endregion
    }
}
