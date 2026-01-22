using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common;
using Core.Pool;
using Core.Window.Blocker;
using Core.Window.Profile;
using Core.Window.Transition;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using ZLinq;
#if VCONTAINER
using VContainer;
using VContainer.Unity;
#endif

namespace Core.Window
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

        // Cache Addressables prefab assets (NOT instances). Instances are handled by PoolManager.
        private readonly Dictionary<Type, GameObject> _prefabCache = new();

        private readonly Stack<UIViewBase> _windowStack = new();
        private readonly Dictionary<UIViewBase, WindowOptions> _windowOptionsMap = new();
        
        private CancellationTokenSource _closeAllCts;

        public UIViewBase TopWindow => _windowStack.Count > 0 ? _windowStack.Peek() : null;
        public int WindowCount => _windowStack.Count;
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
                Debug.LogError("[WindowManager] PoolManager is not available. Window instantiation will fail.");

            if (_inputBlocker == null)
                Debug.LogWarning("[WindowManager] No IUIInputBlocker assigned. Input blocking will be disabled.");

            if (_transitionFactory == null)
                Debug.LogWarning("[WindowManager] No IUITransitionFactory assigned. Transitions will be disabled.");
        }

        protected override void DeinitializeBootstrap()
        {
            _closeAllCts?.Cancel();
            _closeAllCts?.Dispose();
            _closeAllCts = null;

            _windowOptionsMap.Clear();
            _windowStack.Clear();

            // Keep prefab cache by default.
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
                Debug.LogError("[WindowManager] No WindowProfile assigned and none found in resources.");
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
            if (IsTransitioning)
            {
                Debug.LogWarning($"[WindowManager] Cannot open {typeof(TWindow).Name} while transitioning");
                return default;
            }

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
            if (_prefabCache.TryGetValue(type, out var cached) && cached)
                return cached;

            var prefab = await Addressables.LoadAssetAsync<GameObject>(type.Name).ToUniTask();
            if (!prefab)
            {
                Debug.LogError($"[WindowManager] Addressables prefab not found for key '{type.Name}'");
                return null;
            }

            _prefabCache[type] = prefab;
            return prefab;
        }

        private TWindow SpawnWindow<TWindow>(GameObject prefab) where TWindow : UIViewBase
        {
            if (Pool == null)
            {
                Debug.LogError("[WindowManager] Cannot spawn window because PoolManager is null.");
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
                Debug.LogError($"[WindowManager] Spawned prefab '{prefab.name}' does not have component {typeof(TWindow).Name}");
                Pool.Despawn(instance);
                return null;
            }

            instance.SetActive(false);
            return window;
        }

        private async UniTask<TResult> OpenInternal<TResult>(UIViewBase window, WindowOptions options)
        {
            options ??= new WindowOptions();
            ApplyDefaultsFromDefinition(window, options);

            _windowOptionsMap[window] = options;

            if (options.BlockInput && config.blockInputDuringTransitions)
                _inputBlocker?.Block("WindowOpen");

            IsTransitioning = true;

            try
            {
                window.Canvas.overrideSorting = true;
                window.Canvas.sortingOrder = GetSortingOrder(options.WindowType);

                window.Setup(options.Data);

                var closeSource = new UniTaskCompletionSource<object>();
                window.SetCloseSource(closeSource);

                _windowStack.Push(window);
                window.transform.SetAsLastSibling();

                var transition = _transitionFactory?.Create(options.TransitionPreset);
                await window.Show(transition);

                IsTransitioning = false;
                OnWindowOpened?.Invoke(window);

                var result = await closeSource.Task;

                IsTransitioning = true;
                await window.Hide(transition);

                if (_windowStack.Count > 0 && _windowStack.Peek() == window)
                    _windowStack.Pop();

                _windowOptionsMap.Remove(window);
                OnWindowClosed?.Invoke(window);

                // Return instance to pool
                Pool?.Despawn(window.gameObject);

                return (TResult)result;
            }
            finally
            {
                IsTransitioning = false;
                if (options.BlockInput && config.blockInputDuringTransitions)
                    _inputBlocker?.Unblock("WindowOpen");
            }
        }

        private static void ApplyDefaultsFromDefinition(UIViewBase window, WindowOptions options)
        {
            if (window is not IWindowDefinition def) return;
            if (options == null) return;

            // Heuristic merge: if caller left fields at the "common default" values, override from definition.
            // If caller explicitly customized, keep caller values.
            if (options.WindowType == WindowType.Popup)
                options.WindowType = def.WindowType;

            if (string.IsNullOrEmpty(options.TransitionPreset) || options.TransitionPreset == "Scale")
                options.TransitionPreset = def.TransitionPreset;

            if (options.CloseOnBackPress)
                options.CloseOnBackPress = def.CloseOnBackPress;

            if (options.BlockInput)
                options.BlockInput = def.BlockInput;
        }

        #endregion

        #region Close Methods

        public UniTask Close(UIViewBase window)
        {
            if (!_windowStack.Contains(window))
            {
                Debug.LogWarning($"[WindowManager] Window {window.name} not in stack");
                return UniTask.CompletedTask;
            }

            // Close window (will trigger completion source)
            window.OnBackPressed();
            return UniTask.CompletedTask;
        }

        public async UniTask CloseAll(bool immediate = false)
        {
            // Cancel any previous CloseAll operation
            _closeAllCts?.Cancel();
            _closeAllCts?.Dispose();
            _closeAllCts = new CancellationTokenSource();

            var ct = _closeAllCts.Token;

            if (config.blockInputDuringTransitions && !immediate)
                _inputBlocker?.Block("WindowCloseAll");

            try
            {
                // First, complete all pending close sources to prevent waiting
                foreach (var window in _windowStack)
                {
                    window.SetCloseSource(null);
                }

                while (_windowStack.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    var window = _windowStack.Pop();
                    _windowOptionsMap.Remove(window);

                    var transition = immediate ? null : _transitionFactory?.Create("Scale");
                    await window.Hide(transition);

                    OnWindowClosed?.Invoke(window);

                    Pool?.Despawn(window.gameObject);
                }
            }
            catch (OperationCanceledException)
            {
                // CloseAll was cancelled (likely by another CloseAll call)
            }
            finally
            {
                if (config.blockInputDuringTransitions && !immediate)
                    _inputBlocker?.Unblock("WindowCloseAll");
            }
        }

        public UniTask CloseTop()
        {
            if (_windowStack.Count == 0)
                return UniTask.CompletedTask;

            var top = _windowStack.Peek();
            return Close(top);
        }

        #endregion

        #region Query Methods
        
        public bool HasWindow<TWindow>() where TWindow : UIViewBase
        {
            return _windowStack.AsValueEnumerable().OfType<TWindow>().Any();
        }

        public TWindow GetWindow<TWindow>() where TWindow : UIViewBase
        {
            return _windowStack.AsValueEnumerable().OfType<TWindow>().FirstOrDefault();
        }

        #endregion

        #region Helper Methods

        private int GetSortingOrder(WindowType windowType)
        {
            var baseOrder = windowType switch
            {
                WindowType.FullScreenPopup or WindowType.Popup => config.popupBaseSortingOrder,
                WindowType.System => config.systemBaseSortingOrder,
                WindowType.Tutorial => config.systemBaseSortingOrder + 1000,
                _ => config.popupBaseSortingOrder
            };

            return baseOrder + (_windowStack.Count * config.sortingOrderStep);
        }

        #endregion

        #region Input Handling

        private void Update()
        {
            // Android back button / Escape key
            if (Keyboard.current == null) return;
            if (!Keyboard.current[Key.Escape].wasPressedThisFrame) return;
            if (IsTransitioning) return;
            if (_windowStack.Count <= 0) return;

            var topWindow = _windowStack.Peek();
            
            // Check if window allows back press
            if (_windowOptionsMap.TryGetValue(topWindow, out var options) && !options.CloseOnBackPress)
                return;

            topWindow.OnBackPressed();
        }

        #endregion
    }
}

