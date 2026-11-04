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
using UnityEngine;
using UnityEngine.InputSystem;

#if VCONTAINER
using VContainer;
using VContainer.Unity;
#endif

namespace _VuTH.Core.Window
{
    public class WindowManager : VBootstrapManager<WindowManager, IWindowManager>, IWindowManager
    {
        [Header("Config")]
        [SerializeField, ReadOnlyField] private WindowProfile config;
        [SerializeField] private Transform windowRoot;

        // Injected; falls back to the PoolManager singleton without DI.
        private IPoolManager _pool;

        [SerializeField] private Component inputBlockerComponent;
        [SerializeField] private Component transitionFactoryComponent;

        private IUIInputBlocker _inputBlocker;
        private IUITransitionFactory _transitionFactory;
        private readonly IUITransitionRunner _transitionRunner = new UITransitionRunner();

        // Addressables prefab loading + handle caching (created lazily: needs `this` as log origin).
        private WindowPrefabCache _prefabCache;
        private WindowPrefabCache PrefabCache => _prefabCache ??= new WindowPrefabCache(this);

        // Open windows (index 0 = bottom, last = top) + resolved options per window.
        private readonly WindowStack<UIViewBase> _windows = new();

        private readonly SemaphoreSlim _opGate = new SemaphoreSlim(1, 1);

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

        private enum CloseAllMode
        {
            None,
            Graceful,
            Immediate
        }

        private enum CloseReason
        {
            Normal,
            CloseAll,
            Force
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

        private CloseAllMode _closeAllMode = CloseAllMode.None;

        // Track the actual close source being awaited for each window so CloseAll can complete it deterministically.
        private readonly Dictionary<UIViewBase, UniTaskCompletionSource<CloseToken>> _closeSourceMap = new();

        // Track windows that were force-cleaned (despawned externally) so OpenInternal can early-exit.
        private readonly HashSet<UIViewBase> _forceCleaned = new();

        public UIViewBase TopWindow => _windows.Top;
        public int WindowCount => _windows.Count;
        public bool IsTransitioning { get; private set; }

        public event Action<UIViewBase> OnWindowOpened;
        public event Action<UIViewBase> OnWindowClosed;

#if VCONTAINER
        /// <summary>Stores dependencies only; they are used from <see cref="InitializeBootstrap"/> on.</summary>
        [Inject]
        public void Construct(IPoolManager pool)
        {
            _pool = pool;
        }
#endif

        #region Lifecycle

        protected override void InitializeBootstrap()
        {
            EnsureProfile();
            // Resolve dependencies from serialized components
            if (inputBlockerComponent)
                _inputBlocker = inputBlockerComponent.GetComponent<IUIInputBlocker>();

            if (transitionFactoryComponent)
                _transitionFactory = transitionFactoryComponent.GetComponent<IUITransitionFactory>();

            if (!_pool.IsAlive()) _pool = PoolManager.HasInstance ? PoolManager.Instance : null;

            if (_pool == null)
                this.LogError("PoolManager is not available. Window instantiation will fail.");

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

            _windows.Clear();

            ClearPrefabCache();
        }

        public void ReleasePrefab<TWindow>() where TWindow : UIViewBase
        {
            if (_windows.Count > 0)
            {
                this.LogWarning($"ReleasePrefab<{typeof(TWindow).Name}> called while windows are open. " +
                                 "This only releases the prefab asset handle (instances remain alive), but re-open may reload.");
            }

            PrefabCache.Release(typeof(TWindow));
        }

        public void ClearPrefabCache()
        {
            if (_windows.Count > 0)
            {
                this.LogWarning($"ClearPrefabCache called while {_windows.Count} window(s) are open. " +
                                 "This only releases prefab asset handles; open instances remain alive.");
            }

            PrefabCache.Clear();
        }

        private void EnsureProfile()
        {
            if (config != null) return;
            if (WindowProfileUtilities.TryGetProfile(out var profile))
            {
                config = profile;
            }
            else
            {
                this.LogError("No WindowProfile assigned and none found in resources.");
            }
        }

        #endregion

        #region Open Methods

        public async UniTask<TResult> Open<TWindow, TResult>(object data = null) where TWindow : UIViewBase
        {
            return await Open<TWindow, TResult>(new WindowOptions { Data = data });
        }

        public async UniTask Open<TWindow>(object data = null) where TWindow : UIViewBase
        {
            await Open<TWindow, object>(new WindowOptions { Data = data });
        }

        public async UniTask<TResult> Open<TWindow, TResult>(WindowOptions options) where TWindow : UIViewBase
        {
            // Don't block on IsTransitioning; operations are serialized.
            var prefab = await PrefabCache.GetOrLoad(typeof(TWindow));
            if (!prefab)
                return default;

            var window = SpawnWindow<TWindow>(prefab);
            if (!window)
                return default;

            return await OpenInternal<TResult>(window, options);
        }

        private TWindow SpawnWindow<TWindow>(GameObject prefab) where TWindow : UIViewBase
        {
            if (_pool == null)
            {
                this.LogError("Cannot spawn window because PoolManager is null.");
                return null;
            }

            // Use Transform component as spawn handle
            var spawnedTransform = _pool.Spawn(prefab.transform, parent: windowRoot);
            if (!spawnedTransform)
                return null;

            var instance = spawnedTransform.gameObject;
            var window = instance.GetComponent<TWindow>();
            if (!window)
            {
                this.LogError($"Spawned prefab '{prefab.name}' does not have component {typeof(TWindow).Name}");
                _pool.Despawn(instance);
                return null;
            }

            instance.SetActive(false);
            return window;
        }

        private sealed class OpenHandle
        {
            public UIViewBase Window { get; set; }
            public WindowOptions Options { get; set; }
            public UniTaskCompletionSource<CloseToken> CloseSource { get; set; }
        }

        private async UniTask<TResult> OpenInternal<TResult>(UIViewBase window, WindowOptions options)
        {
            // Phase 1 (serialized): register + transition in
            var handle = await RunSerialized(async () =>
            {
                options ??= new WindowOptions();
                if (window is IWindowDefinition def)
                    WindowOptionsResolver.ApplyDefaults(options, def);

                _windows.SetOptions(window, options);

                // NOTE: pair Block/Unblock keys must match.
                if (options.BlockInput.GetValueOrDefault(true) && config.blockInputDuringTransitions)
                    _inputBlocker.OrNull()?.Block("WindowTransition");

                IsTransitioning = true;

                if (!window.gameObject.activeSelf)
                    window.gameObject.SetActive(true);

                if (window.CanvasGroup != null)
                {
                    window.CanvasGroup.alpha = 0f;
                    window.CanvasGroup.interactable = false;
                    window.CanvasGroup.blocksRaycasts = false;
                }

                // IMPORTANT: sorting order should reflect stack index AFTER push.
                _windows.Push(window);

                window.Canvas.overrideSorting = true;
                window.Canvas.sortingOrder = GetSortingOrder(options.WindowType, _windows.Count);

                window.Setup(options.Data);

                var closeSource = new UniTaskCompletionSource<CloseToken>();
                _closeSourceMap[window] = closeSource;

                // Bridge legacy UIViewBase.CloseSource (object) to the manager-owned CloseToken source.
                // This keeps backward compatibility for views that call Close()/ForceClose()/TryRequestClose()
                // and ensures OpenInternal actually observes the close.
                var viewCloseSource = new UniTaskCompletionSource<object>();

                // IMPORTANT: don't allow cancellation/exception on the view close task to become unobserved.
                // If the view completes exceptionally, treat it as a force close.
                UniTask.Void(async () =>
                {
                    try
                    {
                        var result = await viewCloseSource.Task;
                        closeSource.TrySetResult(new CloseToken(CloseReason.Normal, result));
                    }
                    catch (OperationCanceledException)
                    {
                        closeSource.TrySetResult(new CloseToken(CloseReason.Force));
                    }
                    catch (Exception)
                    {
                        closeSource.TrySetResult(new CloseToken(CloseReason.Force));
                    }
                });

                window.SetCloseSource(viewCloseSource);

                window.transform.SetAsLastSibling();

                var transitionIn = options.TransitionInSettings != null
                    ? _transitionFactory?.Create(options.TransitionInSettings)
                    : _transitionFactory?.Create(options.TransitionPreset);

                await _transitionRunner.RunIn(window, transitionIn);

                IsTransitioning = false;
                OnWindowOpened?.Invoke(window);
                window.OnViewShown();

                return new OpenHandle { Window = window, Options = options, CloseSource = closeSource };
            });

            // Phase 2 (no lock): wait for close token
            CloseToken token;
            try
            {
                token = await handle.CloseSource.Task;
            }
            catch
            {
                // Treat unknown failure as force close with no payload.
                token = new CloseToken(CloseReason.Force);
            }

            // Early-exit: if CloseAll(forceCleanup) already despawned this window, don't run out/cleanup twice.
            if (_forceCleaned.Remove(handle.Window))
            {
                _closeSourceMap.Remove(handle.Window);
                _windows.RemoveOptions(handle.Window);
                return default;
            }

            // Phase 3 (serialized): transition out + cleanup
            await RunSerialized(async () =>
            {
                _closeSourceMap.Remove(handle.Window);

                var closeAllModeAtClose = _closeAllMode;

                IsTransitioning = true;

                var skipOutTransition = closeAllModeAtClose == CloseAllMode.Immediate || token.Reason == CloseReason.Force;

                var transitionOut = skipOutTransition
                    ? null
                    : (handle.Options.TransitionOutSettings != null
                        ? _transitionFactory?.Create(handle.Options.TransitionOutSettings)
                        : _transitionFactory?.Create(handle.Options.TransitionPreset));

                await _transitionRunner.RunOut(handle.Window, transitionOut);

                // Robust remove: remove the exact window from stack (not only if it's the top).
                _windows.Remove(handle.Window);

                _windows.RemoveOptions(handle.Window);
                OnWindowClosed?.Invoke(handle.Window);
                handle.Window.OnViewHidden();

                _pool.OrNull()?.Despawn(handle.Window.gameObject);

                // Reset CloseAll mode when there are no windows left.
                if (closeAllModeAtClose != CloseAllMode.None && _windows.Count == 0)
                    _closeAllMode = CloseAllMode.None;

                IsTransitioning = false;

                if (handle.Options.BlockInput.GetValueOrDefault(true) && config.blockInputDuringTransitions)
                    _inputBlocker.OrNull()?.Unblock("WindowTransition");
            });

            // Only return payload for Normal closes. CloseAll/Force returns default.
            if (token.Reason != CloseReason.Normal)
                return default;

            if (token.Payload == null)
                return default;

            return (TResult)token.Payload;
        }

        #endregion

        #region Close Methods

        public UniTask Close(UIViewBase window)
        {
            return Close(window, null);
        }

        public UniTask Close(UIViewBase window, object result)
        {
            // Serialize to keep ordering vs open/close phases deterministic.
            return RunSerialized(() =>
            {
                if (window == null)
                    return UniTask.CompletedTask;

                if (!_windows.Contains(window))
                {
                    this.LogWarning($"Window {window.name} not in stack");
                    return UniTask.CompletedTask;
                }

                // Deterministic: complete close token if managed.
                if (_closeSourceMap.TryGetValue(window, out var src))
                {
                    src.TrySetResult(new CloseToken(CloseReason.Normal, result));
                }
                else
                {
                    // Fallback for non-managed views.
                    if (!window.TryRequestClose(result))
                        window.OnBackPressed();
                }

                return UniTask.CompletedTask;
            });
        }

        public UniTask CloseTop()
        {
            return CloseTop(null);
        }

        public UniTask CloseTop(object result)
        {
            return RunSerialized(() =>
            {
                if (_windows.Count == 0)
                    return UniTask.CompletedTask;

                var top = _windows.Top;

                if (_closeSourceMap.TryGetValue(top, out var src))
                    src.TrySetResult(new CloseToken(CloseReason.Normal, result));
                else if (!top.TryRequestClose(result))
                    top.OnBackPressed();

                return UniTask.CompletedTask;
            });
        }

        public async UniTask CloseAll(bool immediate = false, bool forceCleanup = false)
        {
            await RunSerialized(() =>
            {
                _closeAllMode = immediate ? CloseAllMode.Immediate : CloseAllMode.Graceful;

                var windows = _windows.ToArray();

                // Graceful: signal all managed close sources; windows will clean themselves up through OpenInternal phase 3.
                foreach (var w in windows)
                {
                    if (w == null) continue;
                    if (_closeSourceMap.TryGetValue(w, out var src))
                        src.TrySetResult(new CloseToken(CloseReason.CloseAll));
                }

                if (!forceCleanup)
                    return UniTask.CompletedTask;

                // Immediate force cleanup: despawn everything from snapshot, then clear stack/list once.
                foreach (var w in windows)
                {
                    if (w == null) continue;

                    _forceCleaned.Add(w);
                    _windows.RemoveOptions(w);
                    _closeSourceMap.Remove(w);

                    OnWindowClosed?.Invoke(w);
                    w.OnViewHidden();

                    _pool.OrNull()?.Despawn(w.gameObject);
                }

                _windows.ClearWindows();

                _closeAllMode = CloseAllMode.None;

                return UniTask.CompletedTask;
            });

            await UniTask.Yield();
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

        #endregion

        #region Helper Methods

        private int GetSortingOrder(WindowType windowType, int stackCountAfterPush)
        {
            return WindowSortingOrder.Compute(windowType, stackCountAfterPush,
                config.popupBaseSortingOrder, config.systemBaseSortingOrder, config.sortingOrderStep);
        }

        #endregion

        #region Input Handling

        [Header("Input")]
        [Tooltip("Optional. If set, this InputAction will be used as the Back button (recommended for mobile).")]
        [SerializeField] private InputActionReference backAction;

        private bool _backPressedThisFrame;

        private void OnBackActionPerformed(InputAction.CallbackContext ctx)
        {
            // Mark and consume in Update (keeps all logic serialized + main-thread).
            _backPressedThisFrame = true;
        }

        private void Update()
        {
            // Prefer new Input System action (works for Android/iOS gamepad/back button mappings).
            var backPressed = false;
            if (_backPressedThisFrame)
            {
                backPressed = true;
                _backPressedThisFrame = false;
            }
            else
            {
                // Desktop fallback.
                if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
                    backPressed = true;
            }

            if (!backPressed) return;

            // Enqueue back-press close so it doesn't race with open/close phases.
            // IMPORTANT: this is fire-and-forget; attach a handler so exceptions aren't unobserved.
            RunSerialized(() =>
            {
                if (_windows.Count <= 0) return UniTask.CompletedTask;
                var topWindow = _windows.Top;

                if (_windows.TryGetOptions(topWindow, out var options) && !options.CloseOnBackPress.GetValueOrDefault(true))
                    return UniTask.CompletedTask;

                // Deterministic for managed windows: complete manager-owned close token.
                if (_closeSourceMap.TryGetValue(topWindow, out var src))
                {
                    src.TrySetResult(new CloseToken(CloseReason.Normal));
                }
                else
                {
                    // Fallback for non-managed views.
                    if (!topWindow.TryRequestClose())
                        topWindow.OnBackPressed();
                }

                return UniTask.CompletedTask;
            }).Forget(ex => this.LogError($"RunSerialized(back-press close) faulted: {ex}"));
        }

        #endregion
    }
}


