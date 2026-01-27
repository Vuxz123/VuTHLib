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
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using ZLinq;

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

#if VCONTAINER
        [Inject] public IPoolManager Pool;
#else
        public IPoolManager Pool;
#endif

        [SerializeField] private Component inputBlockerComponent;
        [SerializeField] private Component transitionFactoryComponent;

        private IUIInputBlocker _inputBlocker;
        private IUITransitionFactory _transitionFactory;
        private readonly IUITransitionRunner _transitionRunner = new UITransitionRunner();

        // Cache Addressables prefab handles so we can release them properly.
        private readonly Dictionary<Type, AsyncOperationHandle<GameObject>> _prefabHandleCache = new();


        private readonly Stack<UIViewBase> _windowStack = new();
        // Maintain insertion order list so we can remove arbitrary windows without allocating temp stacks.
        // Index 0 = bottom, last = top.
        private readonly List<UIViewBase> _windowList = new();
        private readonly Dictionary<UIViewBase, WindowOptions> _windowOptionsMap = new();

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

        public UIViewBase TopWindow => _windowList.Count > 0 ? _windowList[^1] : null;
        public int WindowCount => _windowList.Count;
        public bool IsTransitioning { get; private set; }

        public event Action<UIViewBase> OnWindowOpened;
        public event Action<UIViewBase> OnWindowClosed;

#if VCONTAINER
        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            // Provide this manager as a service. No injection required.
            builder.RegisterComponent(this).AsImplementedInterfaces();
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

            Pool ??= PoolManager.Instance;

            if (Pool == null)
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

            _windowOptionsMap.Clear();
            _windowStack.Clear();
            _windowList.Clear();

            ClearPrefabCache();
        }

        public void ReleasePrefab<TWindow>() where TWindow : UIViewBase
        {
            if (_windowStack.Count > 0)
            {
                this.LogWarning($"ReleasePrefab<{typeof(TWindow).Name}> called while windows are open. " +
                                 "This only releases the prefab asset handle (instances remain alive), but re-open may reload.");
            }

            var type = typeof(TWindow);
            if (_prefabHandleCache.TryGetValue(type, out var handle) && handle.IsValid())
            {
                // If still loading, don't release mid-flight.
                // Caller can retry release after load completes.
                if (handle.IsDone)
                {
                    Addressables.Release(handle);
                    _prefabHandleCache.Remove(type);
                }
                else
                {
                    this.LogWarning($"ReleasePrefab<{typeof(TWindow).Name}> skipped because the Addressables handle is still loading.");
                }
            }
            else
            {
                _prefabHandleCache.Remove(type);
            }
        }

        public void ClearPrefabCache()
        {
            if (_windowStack.Count > 0)
            {
                this.LogWarning($"ClearPrefabCache called while {_windowStack.Count} window(s) are open. " +
                                 "This only releases prefab asset handles; open instances remain alive.");
            }

            foreach (var kv in _prefabHandleCache
                         .AsValueEnumerable().Where(kv => kv.Value.IsValid() && kv.Value.IsDone))
            {
                Addressables.Release(kv.Value);
            }

            // Keep any handles that are still loading; they can be cleared later.
            foreach (var kv in _prefabHandleCache
                         .AsValueEnumerable()
                         .Where(kv => kv.Value.IsValid() && !kv.Value.IsDone))
            {
                this.LogWarning($"Skipped releasing prefab handle for {kv.Key.Name} because it is still loading.");
            }

            var keysToRemove = _prefabHandleCache
                .AsValueEnumerable()
                .Where(kv => !kv.Value.IsValid() || kv.Value.IsDone)
                .Select(kv => kv.Key);

            foreach (var key in keysToRemove)
                _prefabHandleCache.Remove(key);
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
            var prefab = await GetOrLoadPrefab<TWindow>();
            if (!prefab)
                return default;

            var window = SpawnWindow<TWindow>(prefab);
            if (!window)
                return default;

            return await OpenInternal<TResult>(window, options);
        }

        private async UniTask<GameObject> GetOrLoadPrefab<TWindow>() where TWindow : UIViewBase
        {
            var type = typeof(TWindow);

            if (_prefabHandleCache.TryGetValue(type, out var cachedHandle) && cachedHandle.IsValid())
            {
                // If handle hasn't completed yet, await completion.
                try
                {
                    if (!cachedHandle.IsDone)
                        await cachedHandle.Task;
                }
                catch (Exception e)
                {
                    this.LogError($"Addressables load faulted for key '{type.Name}': {e}");
                    Addressables.Release(cachedHandle);
                    _prefabHandleCache.Remove(type);
                    return null;
                }

                if (!cachedHandle.Result)
                {
                    this.LogError($"Addressables prefab not found for key '{type.Name}'");
                    Addressables.Release(cachedHandle);
                    _prefabHandleCache.Remove(type);
                    return null;
                }

                return cachedHandle.Result;
            }

            var handle = Addressables.LoadAssetAsync<GameObject>(type.Name);
            _prefabHandleCache[type] = handle;

            try
            {
                await handle.Task;
            }
            catch (Exception e)
            {
                this.LogError($"Addressables load faulted for key '{type.Name}': {e}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                _prefabHandleCache.Remove(type);
                return null;
            }

            if (!handle.Result)
            {
                this.LogError($"Addressables prefab not found for key '{type.Name}'");
                // Avoid keeping a failed/empty handle.
                if (handle.IsValid())
                    Addressables.Release(handle);
                _prefabHandleCache.Remove(type);
                return null;
            }

            return handle.Result;
        }

        private TWindow SpawnWindow<TWindow>(GameObject prefab) where TWindow : UIViewBase
        {
            if (Pool == null)
            {
                this.LogError("Cannot spawn window because PoolManager is null.");
                return null;
            }

            // Use Transform component as spawn handle
            var spawnedTransform = Pool.Spawn(prefab.transform, parent: windowRoot);
            if (!spawnedTransform)
                return null;

            var instance = spawnedTransform.gameObject;
            var window = instance.GetComponent<TWindow>();
            if (!window)
            {
                this.LogError($"Spawned prefab '{prefab.name}' does not have component {typeof(TWindow).Name}");
                Pool.Despawn(instance);
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
                ApplyDefaultsFromDefinition(window, options);

                _windowOptionsMap[window] = options;

                // NOTE: pair Block/Unblock keys must match.
                if (options.BlockInput.GetValueOrDefault(true) && config.blockInputDuringTransitions)
                    _inputBlocker?.Block("WindowTransition");

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
                _windowStack.Push(window);
                _windowList.Add(window);

                window.Canvas.overrideSorting = true;
                window.Canvas.sortingOrder = GetSortingOrder(options.WindowType, _windowList.Count);

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
                _windowOptionsMap.Remove(handle.Window);
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
                // Keep list/stack consistent.
                _windowList.Remove(handle.Window);

                // Rebuild stack from list (bottom->top). This avoids per-close temporary Stack allocations.
                _windowStack.Clear();
                foreach (var t in _windowList)
                    _windowStack.Push(t);

                _windowOptionsMap.Remove(handle.Window);
                OnWindowClosed?.Invoke(handle.Window);
                handle.Window.OnViewHidden();

                Pool?.Despawn(handle.Window.gameObject);

                // Reset CloseAll mode when there are no windows left.
                if (closeAllModeAtClose != CloseAllMode.None && _windowList.Count == 0)
                    _closeAllMode = CloseAllMode.None;

                IsTransitioning = false;

                if (handle.Options.BlockInput.GetValueOrDefault(true) && config.blockInputDuringTransitions)
                    _inputBlocker?.Unblock("WindowTransition");
            });

            // Only return payload for Normal closes. CloseAll/Force returns default.
            if (token.Reason != CloseReason.Normal)
                return default;

            if (token.Payload == null)
                return default;

            return (TResult)token.Payload;
        }

        private static void ApplyDefaultsFromDefinition(UIViewBase window, WindowOptions options)
        {
            if (window is not IWindowDefinition def) return;
            if (options == null) return;

            // Heuristic merge: if caller left fields at the "common default" values, override from definition.
            // If caller explicitly customized, keep caller values.
            if (options.WindowType == WindowType.Popup)
                options.WindowType = def.WindowType;

            // Data-driven settings (preferred)
            options.TransitionInSettings ??= def.TransitionInSettings;
            options.TransitionOutSettings ??= def.TransitionOutSettings;

            // Legacy preset fallback (single preset for both directions)
            if ((options.TransitionInSettings == null && options.TransitionOutSettings == null)
                && (string.IsNullOrEmpty(options.TransitionPreset) || options.TransitionPreset == "Scale"))
            {
                options.TransitionPreset = def.TransitionPreset;
            }

            // Nullable bools: only default from definition when caller didn't specify.
            options.CloseOnBackPress ??= def.CloseOnBackPress;
            options.BlockInput ??= def.BlockInput;
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

                if (!_windowList.Contains(window))
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
                if (_windowList.Count == 0)
                    return UniTask.CompletedTask;

                var top = _windowList[^1];

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

                var windows = _windowList.ToArray();

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
                    _windowOptionsMap.Remove(w);
                    _closeSourceMap.Remove(w);

                    OnWindowClosed?.Invoke(w);
                    w.OnViewHidden();

                    Pool?.Despawn(w.gameObject);
                }

                _windowList.Clear();
                _windowStack.Clear();

                _closeAllMode = CloseAllMode.None;

                return UniTask.CompletedTask;
            });

            await UniTask.Yield();
        }

        #endregion

        #region Query Methods

        public bool HasWindow<TWindow>() where TWindow : UIViewBase
        {
            return _windowList.AsValueEnumerable().OfType<TWindow>().Any();
        }

        public TWindow GetWindow<TWindow>() where TWindow : UIViewBase
        {
            return _windowList.AsValueEnumerable().OfType<TWindow>().FirstOrDefault();
        }

        #endregion

        #region Helper Methods

        private int GetSortingOrder(WindowType windowType, int stackCountAfterPush)
        {
            var baseOrder = windowType switch
            {
                WindowType.FullScreenPopup or WindowType.Popup => config.popupBaseSortingOrder,
                WindowType.System => config.systemBaseSortingOrder,
                WindowType.Tutorial => config.systemBaseSortingOrder + 1000,
                _ => config.popupBaseSortingOrder
            };

            var index = Mathf.Max(0, stackCountAfterPush - 1);
            return baseOrder + (index * config.sortingOrderStep);
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
                if (_windowStack.Count <= 0) return UniTask.CompletedTask;
                var topWindow = _windowStack.Peek();

                if (_windowOptionsMap.TryGetValue(topWindow, out var options) && !options.CloseOnBackPress.GetValueOrDefault(true))
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


