using System;
using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.DI;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.GlobalEvent;
using _VuTH.Core.GameCycle.Screen.Loading;
using _VuTH.Core.GameCycle.Screen.LocalEvents;
using _VuTH.Core.GameCycle.Screen.Progress;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using ZLinq;

namespace _VuTH.Core.GameCycle.Screen
{
    public class ScreenManager : VBootstrapManager<ScreenManager, IScreenManager>, IScreenManager
    {
        [Header("Screen Settings")]
        [SerializeField] private ScreenModelContainer screenContainer;
        
        [Header("Events Settings")]
        [SerializeField] private GlobalScreenEventProfile[] eventProfile;
        
        [Header("Loading Settings")]
        [SerializeField] private float loadingBarSpeed = 0.5f;

        /// <summary>
        /// Optional: expose a unified registration facade so other systems can register listeners without
        /// knowing which profile is used.
        /// </summary>
        public IGlobalScreenEventRegistration GlobalEventRegistration { get; private set; }

        [Header("Loading (Transition UI)")]
        [Tooltip("Optional. Assign a Component that also implements ILoadingController. Loading is NOT a Screen.")]
        [SerializeField] private Component loadingController;

        private ILoadingController _loading;

        // Base (state) screen (Enter changes this)
        private ScreenModel _current;

        // Stack (runtime navigation). Top = effective current.
        private readonly Stack<ScreenModel> _screenStack = new();

        // Override (interrupt). Single slot.
        private ScreenModel _override;

        // Previous effective screen
        private ScreenModel _previous;

        private bool _isTransitioning;

        private readonly LocalScreenEventContainer _localEventContainer = new();

        // Addressables cache: AssetGUID -> SceneInstance
        private readonly Dictionary<string, SceneInstance> _cachedAddressableScenes = new();

        public event Action<TransitionCompletedEventArgs> OnTransitionCompleted;
        
#if VCONTAINER
        private LifetimeScope _coreLifetimeScope;
#endif

        #region IScreenManager

        public ScreenModel Current => GetEffectiveCurrent();
        public ScreenModel Previous => _previous;
        public bool IsTransitioning => _isTransitioning;
        public ILocalScreenEventRegistration LocalEventRegistration => _localEventContainer;

        public bool CanPop => _screenStack.Count > 1;
        public bool IsOverriding => _override != null;

        #endregion

        #region Bootstrap

        protected override void InitializeBootstrap()
        {
#if VCONTAINER
            // Tìm LifetimeScope chứa ScreenManager (thường là Root của Scene Core)
            _coreLifetimeScope = LifetimeScope.Find<RootScopeContainer>();
#endif
            
            if (screenContainer == null || screenContainer.screens == null || screenContainer.screens.Length == 0)
            {
                this.LogError("ScreenManager: No valid ScreenModels found!");
                return;
            }
            screenContainer.SetupContainer(screenContainer.screens);

            _loading = ResolveLoadingController(loadingController);

            GlobalEventRegistration = new GlobalScreenEventHub(eventProfile);

            // Establish a stable baseline screen definition (not dependent on current Unity scene).
            // This prevents Enter() from having no 'from' reference on first transition.
            if (_current == null && screenContainer.bootstrapScreen != null)
            {
                _current = screenContainer.bootstrapScreen;
            }
        }

        protected override void DeinitializeBootstrap()
        {
            _current = null;
            _override = null;
            _screenStack.Clear();
            _previous = null;
            _isTransitioning = false;

            foreach (var kv in _cachedAddressableScenes)
            {
                Addressables.UnloadSceneAsync(kv.Value);
            }
            _cachedAddressableScenes.Clear();

            _loading = null;
            GlobalEventRegistration = null;
        }

        #endregion

        #region Core API

        public async UniTask Enter(ScreenModel target)
        {
            await Enter(target, TransitionContext.Default);
        }

        public async UniTask Enter(ScreenModel target, TransitionContext context)
        {
            this.Log($"Enter({target?.name}) called.");
            if (!target)
            {
                this.LogError("Enter called with null target!");
                return;
            }

            if (_isTransitioning)
            {
                this.LogWarning($"ScreenManager is transitioning. Ignored Enter({target.name})");
                return;
            }

            _isTransitioning = true;

            // Capture current effective before we reset stack.
            var from = GetEffectiveCurrent();

            try
            {
                // Enter starts a NEW flow: clear override + clear stack.
                _override = null;

                // Clear stack unloads all screens in current flow (top-down).
                await ClearStackInternal();

                // IMPORTANT: if there was a base current screen but it wasn't in stack (e.g. initial bootstrap baseline),
                // we must close it too. Otherwise Enter() will keep old main scene loaded (additive) forever.
                if (from && from != target)
                {
                    await UnloadScreenInternal(from, nextToShow: null);
                }

                _previous = from;

                // Now set the new base + push it as the only stack item.
                _current = target;
                _screenStack.Push(target);

                // Loading is a Transition UI concern (NOT a Screen).
                await TransitionWithLoadingInternal(from, target, loadMainScene: true, showLoading: target.showLoadingScreen);
            }
            finally
            {
                _isTransitioning = false;
            }

            OnTransitionCompleted?.Invoke(new TransitionCompletedEventArgs(TransitionKind.Enter, from, target, context));
        }

        public UniTask Enter(ScreenIdentifier id)
        {
            return Enter(id, TransitionContext.Default);
        }

        public UniTask Enter(ScreenIdentifier id, TransitionContext context)
        {
            var target = screenContainer.GetScreenById(id);
            return Enter(target, context);
        }

        public async UniTask Push(ScreenModel target)
        {
            await Push(target, TransitionContext.Default);
        }

        public async UniTask Push(ScreenModel target, TransitionContext context)
        {
            if (!target)
            {
                this.LogError("Push called with null target!");
                return;
            }

            if (_isTransitioning)
            {
                this.LogWarning($"ScreenManager is transitioning. Ignored Push({target.name})");
                return;
            }

            if (_override)
            {
                this.LogWarning($"Ignored Push({target.name}) because an override screen is active ({_override.name}). PopOverride first.");
                return;
            }

            var from = GetEffectiveCurrent();
            if (from == target)
            {
                this.Log($"Push ignored because {target.name} is already effective.");
                return;
            }

            _isTransitioning = true;
            try
            {
                _screenStack.Push(target);
                _previous = from;

                await TransitionToInternal(from, target, loadMainScene: true);
            }
            catch
            {
                if (_screenStack.Count > 0 && _screenStack.Peek() == target) _screenStack.Pop();
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }

            OnTransitionCompleted?.Invoke(new TransitionCompletedEventArgs(TransitionKind.Push, from, target, context));
        }

        public async UniTask Pop()
        {
            await Pop(TransitionContext.Default);
        }

        public async UniTask Pop(TransitionContext context)
        {
            if (_isTransitioning)
            {
                this.LogWarning("ScreenManager is transitioning. Ignored Pop()");
                return;
            }

            if (_override)
            {
                this.LogWarning($"Ignored Pop() because an override screen is active ({_override.name}). PopOverride first.");
                return;
            }

            if (_screenStack.Count <= 1)
            {
                this.LogWarning("Pop() called but stack has no previous screen.");
                return;
            }

            var curr = _screenStack.Pop();
            var prev = _screenStack.Peek();

            _isTransitioning = true;
            try
            {
                _previous = curr;

                var exitArgs = new ScreenEventArgs(curr, prev);
                NotifyGlobalPreExit(exitArgs);
                _localEventContainer.NotifyScreenClosing(exitArgs);

                await UnloadScreenInternal(curr, nextToShow: prev);
                ShowScreenInternal(prev);

                NotifyGlobalPostExit(exitArgs);

                var enterArgs = new ScreenEventArgs(curr, prev);
                NotifyGlobalPreEnter(enterArgs);
                _localEventContainer.NotifyScreenOpening(enterArgs);
                NotifyGlobalPostEnter(enterArgs);

                _current = GetBottomOfStack();
            }
            catch
            {
                _screenStack.Push(curr);
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }

            OnTransitionCompleted?.Invoke(new TransitionCompletedEventArgs(TransitionKind.Pop, curr, prev, context));
        }

        public async UniTask PushOverride(ScreenModel target)
        {
            await PushOverride(target, TransitionContext.Default);
        }

        public async UniTask PushOverride(ScreenModel target, TransitionContext context)
        {
            if (!target)
            {
                this.LogError("PushOverride called with null target!");
                return;
            }

            if (_isTransitioning)
            {
                this.LogWarning($"ScreenManager is transitioning. Ignored PushOverride({target.name})");
                return;
            }

            var from = GetEffectiveCurrent();
            if (from == target)
            {
                this.Log($"PushOverride ignored because {target.name} is already effective.");
                return;
            }

            _isTransitioning = true;
            var prevOverride = _override;
            try
            {
                _override = target;
                _previous = from;

                // Override may show loading, but still controlled by ScreenManager.
                await TransitionWithLoadingInternal(from, target, loadMainScene: true, showLoading: target.showLoadingScreen);
            }
            catch
            {
                _override = prevOverride;
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }

            OnTransitionCompleted?.Invoke(new TransitionCompletedEventArgs(TransitionKind.PushOverride, from, target, context));
        }

        public async UniTask PopOverride()
        {
            await PopOverride(TransitionContext.Default);
        }

        public async UniTask PopOverride(TransitionContext context)
        {
            if (_isTransitioning)
            {
                this.LogWarning("ScreenManager is transitioning. Ignored PopOverride()");
                return;
            }

            if (!_override)
            {
                this.LogWarning("PopOverride() called but no override is active.");
                return;
            }

            var exitingOverride = _override;
            _override = null;
            var to = GetEffectiveCurrent();

            _isTransitioning = true;
            try
            {
                var exitArgs = new ScreenEventArgs(exitingOverride, to);
                NotifyGlobalPreExit(exitArgs);
                _localEventContainer.NotifyScreenClosing(exitArgs);

                await UnloadScreenInternal(exitingOverride, nextToShow: to);
                ShowScreenInternal(to);

                NotifyGlobalPostExit(exitArgs);

                var enterArgs = new ScreenEventArgs(exitingOverride, to);
                NotifyGlobalPreEnter(enterArgs);
                _localEventContainer.NotifyScreenOpening(enterArgs);
                NotifyGlobalPostEnter(enterArgs);
            }
            finally
            {
                _isTransitioning = false;
            }

            OnTransitionCompleted?.Invoke(new TransitionCompletedEventArgs(TransitionKind.PopOverride, exitingOverride, to, context));
        }

        #endregion

        #region Effective helpers

        private ScreenModel GetEffectiveCurrent()
        {
            if (_override) return _override;
            return _screenStack.Count > 0 ? _screenStack.Peek() : _current;
        }

        private ScreenModel GetBottomOfStack()
        {
            var arr = _screenStack.ToArray();
            return arr.Length > 0 ? arr[^1] : null;
        }

        private async UniTask ClearStackInternal()
        {
            if (_screenStack.Count == 0) return;

            // We'll unload everything from top to bottom.
            // Note: Stack.ToArray() returns [top..bottom]. Perfect for closing overlays first.
            var screens = _screenStack.ToArray();
            _screenStack.Clear();

            foreach (var t in screens)
            {
                // Since we're clearing flow entirely, there is no next screen to keep for smart look-ahead.
                await UnloadScreenInternal(t, nextToShow: null);
            }
        }

        #endregion

        #region Addressables load/unload

        private async UniTask TransitionWithLoadingInternal(ScreenModel from, ScreenModel to, bool loadMainScene, bool showLoading)
        {
            if (from)
            {
                var exitArgs = new ScreenEventArgs(from, to);
                NotifyGlobalPreExit(exitArgs);
                _localEventContainer.NotifyScreenClosing(exitArgs);
            }

            if (showLoading)
            {
                await _loading.Show();
                _loading.SetProgress(0f);
            }

            try
            {
                // Pre-enter before any heavy work for the target.
                if (to)
                {
                    var enterArgs = new ScreenEventArgs(from, to);
                    NotifyGlobalPreEnter(enterArgs);
                }

                await LoadScreenInternal(to, loadMainScene, showLoading);
                if (showLoading) _loading.SetProgress(1f);

                ShowScreenInternal(to);

                if (to)
                {
                    var enterArgs = new ScreenEventArgs(from, to);
                    _localEventContainer.NotifyScreenOpening(enterArgs);
                    NotifyGlobalPostEnter(enterArgs);
                }
            }
            finally
            {
                if (showLoading)
                {
                    await _loading.Hide();
                }
            }

            if (from)
            {
                var exitArgs = new ScreenEventArgs(from, to);
                NotifyGlobalPostExit(exitArgs);
            }
        }

        private async UniTask TransitionToInternal(ScreenModel from, ScreenModel to, bool loadMainScene)
        {
            if (from)
            {
                var exitArgs = new ScreenEventArgs(from, to);
                NotifyGlobalPreExit(exitArgs);
                _localEventContainer.NotifyScreenClosing(exitArgs);
                NotifyGlobalPostExit(exitArgs);
            }

            if (to)
            {
                var enterArgs = new ScreenEventArgs(from, to);
                NotifyGlobalPreEnter(enterArgs);

                await LoadScreenInternal(to, loadMainScene, showLoading: false);
                ShowScreenInternal(to);

                _localEventContainer.NotifyScreenOpening(enterArgs);
                NotifyGlobalPostEnter(enterArgs);
            }
        }

        private async UniTask LoadScreenInternal(ScreenModel screen, bool loadMainScene, bool showLoading)
        {
            if (!screen) return;

            var additiveSceneAddressableDatas = screen.additiveScene;
            var screenMainSceneRef = screen.mainSceneRef;

            AbstractProgressReporter progress = showLoading ? 
                new ProgressReporter(_loading, loadingBarSpeed) : 
                new NonLoadingProgressReporter(_loading);

            // 1) Load scenes
            Scene mainScene = default;
            var additiveScenes = new List<Scene>();
            
            var totalSteps = (loadMainScene ? screenMainSceneRef != null ? 1 : 0 : 0) + (additiveSceneAddressableDatas?.Length ?? 0);
            var stepSceneLoadingProgress = 0.5f / Mathf.Max(1, totalSteps);
            var currentProgress = 0f;

            if (loadMainScene && screenMainSceneRef != null && screenMainSceneRef.RuntimeKeyIsValid())
            {
                await LoadOrShowSceneInternal(screenMainSceneRef);
                currentProgress += stepSceneLoadingProgress;
                progress.Report(currentProgress);
                mainScene = GetLoadedScene(screenMainSceneRef);
            }
            
            if (additiveSceneAddressableDatas != null)
            {
                foreach (var additive in additiveSceneAddressableDatas)
                {
                    if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid())
                        continue;

                    await LoadOrShowSceneInternal(additive.sceneRef);
                    
                    currentProgress += stepSceneLoadingProgress;
                    progress.Report(currentProgress);
                    
                    var loaded = GetLoadedScene(additive.sceneRef);
                    if (loaded.IsValid()) additiveScenes.Add(loaded);
                }
            }

            // 2) Run preloading tasks (new LoadingTask API: AggregateTask + LoadingContext + LoadingHandler)
            if (screen.preloadingTasks is { Length: > 0 })
            {
                var context = new LoadingContext(screen, mainScene, additiveScenes.ToArray());

                var instancedPreloadTasks = screen.preloadingTasks
                    .AsValueEnumerable().Select(Instantiate).Where(taskAsset => taskAsset).ToList();

                // totalUnits = sum of each task's AggregateTask(context), fallback to 1/unit per task.
                var totalUnits = 0;
                foreach (var taskAsset in instancedPreloadTasks.AsValueEnumerable())
                {
                    try
                    {
                        totalUnits += Mathf.Max(0, taskAsset.AggregateTask(context));
                    }
                    catch (Exception e)
                    {
                        this.LogError($"Preloading task AggregateTask failed on '{taskAsset.name}': {e}");
                    }
                }

                if (totalUnits <= 0)
                    totalUnits = 1;

                var handler = new LoadingHandler(progress, totalUnits, 0.5f);

                // Run sequentially (safer for shared services)
                foreach (var taskAsset in instancedPreloadTasks.AsValueEnumerable())
                {
                    try
                    {
                        await taskAsset.Execute(context, handler);
                        taskAsset.Log("Execute completed successfully.");
                    }
                    catch (Exception e)
                    {
                        this.LogError($"Preloading task Execute failed on '{taskAsset.name}': {e}");
                        Debug.LogException(e);

                        // Prevent stuck loading bar by advancing at least 1 unit.
                        handler.Increment();
                    }
                }
            }
            else
            {
                progress.Report(1f);
            }

            await progress.CompleteAsync();
        }

        private Scene GetLoadedScene(AssetReference sceneRef)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key))
                return default;

            return _cachedAddressableScenes.TryGetValue(key, out var cached) ? cached.Scene : default;
        }

        private async UniTask UnloadScreenInternal(ScreenModel screen, ScreenModel nextToShow)
        {
            if (!screen) return;

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid())
            {
                await UnloadSceneByPolicyInternal(screen.mainSceneRef, screen, nextToShow, alwaysCheckSmartLookAhead: false);
            }

            if (screen.additiveScene == null) return;

            foreach (var additive in screen.additiveScene)
            {
                if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid())
                    continue;

                await UnloadSceneByPolicyInternal(additive.sceneRef, screen, nextToShow, alwaysCheckSmartLookAhead: true, unloadOnClose: additive.unloadOnClose);
            }
        }

        private async UniTask LoadOrShowSceneInternal(AssetReference sceneRef)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key))
            {
                this.LogError("Addressables sceneRef has no AssetGUID (is it an addressable scene asset?)");
                return;
            }

            if (_cachedAddressableScenes.TryGetValue(key, out var cached))
            {
                if (cached.Scene.isLoaded)
                {
                    SetSceneRootActive(cached.Scene, true);
                    return;
                }

                _cachedAddressableScenes.Remove(key);
            }

#if VCONTAINER
            if (_coreLifetimeScope == null)
            {
                this.LogError("ScreenManager: Core LifetimeScope is null. Addressable scene loading with VContainer may fail to inject dependencies.");
            }
            // Đưa container của Core vào hàng đợi. 
            // Khi Scene mới load và LifetimeScope của nó Awake, nó sẽ tự động lấy cái này làm Parent.
            using (LifetimeScope.EnqueueParent(_coreLifetimeScope))
            {
#endif
                var handle = Addressables.LoadSceneAsync(sceneRef, LoadSceneMode.Additive, activateOnLoad: true);
                await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    this.LogError($"Failed to load addressable scene: {sceneRef.RuntimeKey}");
                    return;
                }

                _cachedAddressableScenes[key] = handle.Result;
                SetSceneRootActive(handle.Result.Scene, true);
#if VCONTAINER
            }
#endif
        }

        private async UniTask UnloadSceneByPolicyInternal(
            AssetReference sceneRef,
            ScreenModel ownerScreen,
            ScreenModel nextToShow,
            bool alwaysCheckSmartLookAhead,
            bool unloadOnClose = true)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key)) return;

            if (ownerScreen != null && ownerScreen.softCache)
            {
                SetSceneRootActive(sceneRef, false);
                return;
            }

            if (alwaysCheckSmartLookAhead && nextToShow != null)
            {
                if (IsSceneUsedByScreen(nextToShow, key))
                {
                    this.Log($"[Smart Keep] Scene {key} is used by next screen ({nextToShow.name}). Skipping unload.");
                    return;
                }
            }

            if (!unloadOnClose) return;

            if (_cachedAddressableScenes.TryGetValue(key, out var instance))
            {
                await Addressables.UnloadSceneAsync(instance).Task;
                _cachedAddressableScenes.Remove(key);
            }
        }

        #endregion

        #region Helpers

        private void NotifyGlobalPreExit(ScreenEventArgs args)
        {
            if (eventProfile == null) return;
            foreach (var t in eventProfile)
            {
                t?.NotifyPreScreenExit(args);
            }
        }

        private void NotifyGlobalPostExit(ScreenEventArgs args)
        {
            if (eventProfile == null) return;
            foreach (var t in eventProfile)
            {
                t?.NotifyPostScreenExit(args);
            }
        }

        private void NotifyGlobalPreEnter(ScreenEventArgs args)
        {
            if (eventProfile == null) return;
            foreach (var t in eventProfile)
            {
                t?.NotifyPreScreenEnter(args);
            }
        }

        private void NotifyGlobalPostEnter(ScreenEventArgs args)
        {
            if (eventProfile == null) return;
            foreach (var t in eventProfile)
            {
                t?.NotifyPostScreenEnter(args);
            }
        }

        private static string GetAssetGuid(AssetReference reference) => reference?.AssetGUID;

        private static bool IsSceneUsedByScreen(ScreenModel screen, string assetGuid)
        {
            if (!screen) return false;

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid() && GetAssetGuid(screen.mainSceneRef) == assetGuid)
                return true;

            if (screen.additiveScene == null) return false;

            return screen.additiveScene.AsValueEnumerable()
                .Any(x => x.sceneRef != null && x.sceneRef.RuntimeKeyIsValid() && GetAssetGuid(x.sceneRef) == assetGuid);
        }

        private void SetSceneRootActive(AssetReference sceneRef, bool active)
        {
            var key = GetAssetGuid(sceneRef);
            if (string.IsNullOrEmpty(key)) return;

            if (_cachedAddressableScenes.TryGetValue(key, out var instance))
            {
                SetSceneRootActive(instance.Scene, active);
            }
        }

        private static void SetSceneRootActive(Scene scene, bool active)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = scene.GetRootGameObjects();
            foreach (var t in roots)
            {
                if (t) t.SetActive(active);
            }
        }

        private static ILoadingController ResolveLoadingController(Component candidate)
        {
            if (!candidate) return new NullLoadingController();

            var behaviours = candidate.GetComponents<ILoadingController>();
            
            switch (behaviours.Length)
            {
                case 0:
                    Debug.LogError("ScreenManager: Assigned loadingController does not implement ILoadingController. Using NullLoadingController.");
                    return new NullLoadingController();
                case > 1:
                    Debug.LogWarning("ScreenManager: Assigned loadingController has multiple ILoadingController components. Using the first one.");
                    break;
            }

            return behaviours[0];
        }

        /// <summary>
        /// Unified global event registration to wire multiple GlobalScreenEventProfiles from the inspector.
        /// </summary>
        private sealed class GlobalScreenEventHub : IGlobalScreenEventRegistration
        {
            private readonly GlobalScreenEventProfile[] _profiles;

            public GlobalScreenEventHub(GlobalScreenEventProfile[] profiles)
            {
                _profiles = profiles;
            }

            public void RegisterListener(IScreenEventListener listener)
            {
                if (_profiles == null) return;
                foreach (var t in _profiles)
                {
                    t?.RegisterListener(listener);
                }
            }

            public void UnregisterListener(IScreenEventListener listener)
            {
                if (_profiles == null) return;
                foreach (var t in _profiles)
                {
                    t?.UnregisterListener(listener);
                }
            }
        }

        private void ShowScreenInternal(ScreenModel screen)
        {
            if (!screen) return;

            if (screen.mainSceneRef != null && screen.mainSceneRef.RuntimeKeyIsValid())
                SetSceneRootActive(screen.mainSceneRef, true);

            if (screen.additiveScene == null) return;

            foreach (var additive in screen.additiveScene)
            {
                if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid())
                    continue;

                SetSceneRootActive(additive.sceneRef, true);
            }
        }

        #endregion
    }
}
