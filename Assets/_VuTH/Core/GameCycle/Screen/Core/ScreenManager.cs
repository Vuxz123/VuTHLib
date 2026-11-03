using System;
using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Events.Global;
using _VuTH.Core.GameCycle.Screen.Events.Local;
using _VuTH.Core.GameCycle.Screen.Events.MessagePipe;
using _VuTH.Core.GameCycle.Screen.Identifier;
using _VuTH.Core.GameCycle.Screen.Loading;
using _VuTH.Core.GameCycle.Screen.Loading.Controllers;
using _VuTH.Core.GameCycle.Screen.Progress;
using _VuTH.Core.GameCycle.Screen.Transition;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using _VuTH.Common.MessagePipe;
using ZLinq;
#if VCONTAINER
using VContainer;
using VContainer.Unity;
using _VuTH.Common.DI;
#endif

namespace _VuTH.Core.GameCycle.Screen.Core
{
    /// <summary>
    /// Coordinates screen transitions (Enter / Push / Pop / Override).
    /// Navigation state lives in <see cref="ScreenNavigationState"/>, event fan-out in <see cref="ScreenEventDispatcher"/>
    /// and Addressables scene caching in <see cref="AddressableSceneCache"/>.
    /// </summary>
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

        private ILoadingController _loading = new NullLoadingController();

        // Base / stack / override / previous navigation state.
        private readonly ScreenNavigationState _nav = new();

        private bool _isTransitioning;

        private readonly LocalScreenEventContainer _localEventContainer = new();

        // Global / local / MessagePipe event fan-out (created lazily: needs instance members).
        private ScreenEventDispatcher _eventDispatcher;
        private ScreenEventDispatcher EventDispatcher =>
            _eventDispatcher ??= new ScreenEventDispatcher(() => eventProfile, _localEventContainer);

        // Addressables cache: AssetGUID -> SceneInstance (created lazily: uses this as log origin).
        private AddressableSceneCache _sceneCache;
        private AddressableSceneCache SceneCache => _sceneCache ??= new AddressableSceneCache(this);

        public event Action<TransitionCompletedEventArgs> OnTransitionCompleted;

        #region IScreenManager

        public ScreenModel Current => _nav.EffectiveCurrent;
        public ScreenModel Previous => _nav.Previous;
        public bool IsTransitioning => _isTransitioning;
        public ILocalScreenEventRegistration LocalEventRegistration => _localEventContainer;

        public bool CanPop => _nav.CanPop;
        public bool IsOverriding => _nav.IsOverriding;

        #endregion

        #region Bootstrap

#if VCONTAINER
        [Inject]
        public void Construct(IPublisher<PreScreenEnterEvent> preEnterPublisher,
            IPublisher<PostScreenEnterEvent> postEnterPublisher,
            IPublisher<PreScreenExitEvent> preExitPublisher,
            IPublisher<PostScreenExitEvent> postExitPublisher,
            IPublisher<ScreenTransitionCompletedEvent> transitionCompletedPublisher)
        {
            EventDispatcher.PreEnterPublisher = preEnterPublisher;
            EventDispatcher.PostEnterPublisher = postEnterPublisher;
            EventDispatcher.PreExitPublisher = preExitPublisher;
            EventDispatcher.PostExitPublisher = postExitPublisher;
            EventDispatcher.TransitionCompletedPublisher = transitionCompletedPublisher;
        }
#endif

        protected override void InitializeBootstrap()
        {
#if VCONTAINER
            // Tìm LifetimeScope chứa ScreenManager (thường là Root của Scene Core)
            SceneCache.CoreLifetimeScope = LifetimeScope.Find<RootScopeContainer>();
#endif

            if (screenContainer?.screens == null || screenContainer.screens.Length == 0)
            {
                this.LogError("ScreenManager: No valid ScreenModels found!");
                return;
            }
            screenContainer.SetupContainer(screenContainer.screens);

            _loading = ResolveLoadingController(loadingController);

            GlobalEventRegistration = new GlobalScreenEventHub(eventProfile);

            // Initialize MessagePipe publishers
            InitializeMessagePipePublishers();

            // Establish a stable baseline screen definition (not dependent on current Unity scene).
            // This prevents Enter() from having no 'from' reference on first transition.
            if (_nav.Base == null && screenContainer.BaselineScreen != null)
            {
                _nav.Base = screenContainer.BaselineScreen;
            }
        }

        private void InitializeMessagePipePublishers()
        {
#if !VCONTAINER
            try
            {
                EventDispatcher.PreEnterPublisher = MessagePipeHelper.GetPublisher<PreScreenEnterEvent>();
                EventDispatcher.PostEnterPublisher = MessagePipeHelper.GetPublisher<PostScreenEnterEvent>();
                EventDispatcher.PreExitPublisher = MessagePipeHelper.GetPublisher<PreScreenExitEvent>();
                EventDispatcher.PostExitPublisher = MessagePipeHelper.GetPublisher<PostScreenExitEvent>();
                EventDispatcher.TransitionCompletedPublisher = MessagePipeHelper.GetPublisher<ScreenTransitionCompletedEvent>();
            }
            catch (Exception e)
            {
                this.LogWarning($"ScreenManager: Failed to initialize MessagePipe publishers: {e.Message}. MessagePipe events will not be published.");
            }
#endif
        }

        protected override void DeinitializeBootstrap()
        {
            _nav.Reset();
            _isTransitioning = false;

            SceneCache.ReleaseAll();

            _loading = new NullLoadingController();
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
            this.Log($"Enter({(target != null ? target.name : "null")}) called.");
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
            var from = _nav.EffectiveCurrent;

            // Enter starts a NEW flow: snapshot everything that has to be closed (stack top-down + override),
            // then reset navigation to [target].
            var stackToClose = _nav.TakeAll();
            _nav.Override = null;
            _nav.Previous = from;
            _nav.Base = target;
            _nav.Push(target);

            try
            {
                // Old screens are closed INSIDE the transition: after PreExit has been raised (their listeners still
                // exist) and after the loading UI is up (no empty frames between unload and loading screen).
                // Shared additive scenes are kept via smart look-ahead on the target.
                await TransitionWithLoadingInternal(from, target, showLoading: target.showLoadingScreen,
                    closePrevious: async () =>
                    {
                        foreach (var screen in stackToClose)
                        {
                            await SceneCache.UnloadScreenAsync(screen, nextToShow: target);
                        }

                        // A baseline screen that was never pushed (e.g. bootstrap) or an active override.
                        if (from && from != target && Array.IndexOf(stackToClose, from) < 0)
                        {
                            await SceneCache.UnloadScreenAsync(from, nextToShow: target);
                        }
                    });
            }
            catch (Exception e)
            {
                // Old screens may already be unloaded, so there is nothing consistent to roll back to:
                // navigation stays on the target. Callers (e.g. ScreenFlowActor) get the exception.
                this.LogError($"Enter({target.name}) failed: {e.Message}");
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }

            NotifyTransitionCompleted(TransitionKind.Enter, from, target, context);
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

        public UniTask Push(ScreenModel target, TransitionContext context)
        {
            return Push(target, target && target.hideScreenBelow, context);
        }

        public async UniTask Push(ScreenModel target, bool hideScreenBelow, TransitionContext context)
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

            if (_nav.Override)
            {
                this.LogWarning($"Ignored Push({target.name}) because an override screen is active ({_nav.Override.name}). PopOverride first.");
                return;
            }

            var from = _nav.EffectiveCurrent;
            if (from == target)
            {
                this.Log($"Push ignored because {target.name} is already effective.");
                return;
            }

            _isTransitioning = true;
            try
            {
                _nav.Push(target);
                _nav.Previous = from;

                // Push never shows the loading UI and keeps the screen below loaded (stack semantics).
                await TransitionWithLoadingInternal(from, target, showLoading: false);

                // Hide only after the new screen is visible (no empty frame). Pop shows it again.
                if (hideScreenBelow && from)
                {
                    SceneCache.HideScreen(from, keepVisibleFor: target);
                }
            }
            catch
            {
                _nav.PopIfTop(target);
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }

            NotifyTransitionCompleted(TransitionKind.Push, from, target, context);
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

            if (_nav.Override)
            {
                this.LogWarning($"Ignored Pop() because an override screen is active ({_nav.Override.name}). PopOverride first.");
                return;
            }

            if (_nav.StackCount <= 1)
            {
                this.LogWarning("Pop() called but stack has no previous screen.");
                return;
            }

            var curr = _nav.Pop();
            var prev = _nav.Peek();

            _isTransitioning = true;
            try
            {
                _nav.Previous = curr;

                var exitArgs = new ScreenEventArgs(curr, prev);
                EventDispatcher.NotifyGlobalPreExit(exitArgs);
                EventDispatcher.NotifyLocalClosing(exitArgs);

                await SceneCache.UnloadScreenAsync(curr, nextToShow: prev);
                SceneCache.ShowScreen(prev);

                EventDispatcher.NotifyGlobalPostExit(exitArgs);

                var enterArgs = new ScreenEventArgs(curr, prev);
                EventDispatcher.NotifyGlobalPreEnter(enterArgs);
                EventDispatcher.NotifyLocalOpening(enterArgs);
                EventDispatcher.NotifyGlobalPostEnter(enterArgs);

                _nav.Base = _nav.BottomOfStack;
            }
            catch
            {
                _nav.Push(curr);
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }

            NotifyTransitionCompleted(TransitionKind.Pop, curr, prev, context);
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

            var from = _nav.EffectiveCurrent;
            if (from == target)
            {
                this.Log($"PushOverride ignored because {target.name} is already effective.");
                return;
            }

            _isTransitioning = true;
            var prevOverride = _nav.Override;
            var prevOverrideClosed = false;
            try
            {
                _nav.Override = target;
                _nav.Previous = from;

                // Replacing an active override: close it, otherwise its scenes stay loaded forever
                // (PopOverride only closes the current override).
                Func<UniTask> closePrevious = null;
                if (prevOverride)
                {
                    closePrevious = async () =>
                    {
                        await SceneCache.UnloadScreenAsync(prevOverride, nextToShow: target);
                        prevOverrideClosed = true;
                    };
                }

                // Override may show loading, but still controlled by ScreenManager.
                await TransitionWithLoadingInternal(from, target, showLoading: target.showLoadingScreen, closePrevious);
            }
            catch
            {
                _nav.Override = prevOverrideClosed ? null : prevOverride;
                throw;
            }
            finally
            {
                _isTransitioning = false;
            }

            NotifyTransitionCompleted(TransitionKind.PushOverride, from, target, context);
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

            if (!_nav.Override)
            {
                this.LogWarning("PopOverride() called but no override is active.");
                return;
            }

            var exitingOverride = _nav.Override;
            _nav.Override = null;
            var to = _nav.EffectiveCurrent;

            _isTransitioning = true;
            try
            {
                var exitArgs = new ScreenEventArgs(exitingOverride, to);
                EventDispatcher.NotifyGlobalPreExit(exitArgs);
                EventDispatcher.NotifyLocalClosing(exitArgs);

                await SceneCache.UnloadScreenAsync(exitingOverride, nextToShow: to);
                SceneCache.ShowScreen(to);

                EventDispatcher.NotifyGlobalPostExit(exitArgs);

                var enterArgs = new ScreenEventArgs(exitingOverride, to);
                EventDispatcher.NotifyGlobalPreEnter(enterArgs);
                EventDispatcher.NotifyLocalOpening(enterArgs);
                EventDispatcher.NotifyGlobalPostEnter(enterArgs);
            }
            finally
            {
                _isTransitioning = false;
            }

            NotifyTransitionCompleted(TransitionKind.PopOverride, exitingOverride, to, context);
        }

        #endregion

        #region Transition / loading orchestration

        /// <summary>
        /// The single transition pipeline used by Enter / Push / PushOverride. Event order is always:
        /// <c>PreExit(from) -> [close previous] -> PostExit(from) -> PreEnter(to) -> [load + show] -> PostEnter(to)</c>
        /// (same order as Pop / PopOverride). With <paramref name="showLoading"/>, the loading UI is shown right after
        /// PreExit - before anything is unloaded - and hidden at the end, even on failure.
        /// </summary>
        private async UniTask TransitionWithLoadingInternal(ScreenModel from, ScreenModel to, bool showLoading,
            Func<UniTask> closePrevious = null)
        {
            var args = new ScreenEventArgs(from, to);

            if (from)
            {
                EventDispatcher.NotifyGlobalPreExit(args);
                EventDispatcher.NotifyLocalClosing(args);
            }

            if (showLoading)
            {
                await _loading.Show();
                _loading.SetProgress(0f);
            }

            try
            {
                if (closePrevious != null)
                {
                    await closePrevious();
                }

                if (from)
                {
                    EventDispatcher.NotifyGlobalPostExit(args);
                }

                if (!to) return;

                EventDispatcher.NotifyGlobalPreEnter(args);

                await LoadScreenInternal(to, loadMainScene: true, showLoading);
                if (showLoading) _loading.SetProgress(1f);

                SceneCache.ShowScreen(to);

                EventDispatcher.NotifyLocalOpening(args);
                EventDispatcher.NotifyGlobalPostEnter(args);
            }
            finally
            {
                if (showLoading)
                {
                    await _loading.Hide();
                }
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

            // Scopes built while these scenes load belong to this screen (Screen-scoped MessagePipe events).
            ScreenSceneMembership.Loading = screen;
            try
            {
                if (loadMainScene && screenMainSceneRef != null && screenMainSceneRef.RuntimeKeyIsValid())
                {
                    await SceneCache.LoadOrShowSceneAsync(screenMainSceneRef);
                    currentProgress += stepSceneLoadingProgress;
                    progress.Report(currentProgress);
                    mainScene = SceneCache.GetLoadedScene(screenMainSceneRef);
                }

                if (additiveSceneAddressableDatas != null)
                {
                    foreach (var additive in additiveSceneAddressableDatas)
                    {
                        if (additive.sceneRef == null || !additive.sceneRef.RuntimeKeyIsValid())
                            continue;

                        await SceneCache.LoadOrShowSceneAsync(additive.sceneRef);

                        currentProgress += stepSceneLoadingProgress;
                        progress.Report(currentProgress);

                        var loaded = SceneCache.GetLoadedScene(additive.sceneRef);
                        if (loaded.IsValid()) additiveScenes.Add(loaded);
                    }
                }
            }
            finally
            {
                ScreenSceneMembership.Loading = null;
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

                try
                {
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

                            // Advance the bar; CompleteAsync below drives it to 100% regardless of unit counts.
                            handler.Increment();
                        }
                    }
                }
                finally
                {
                    // Instances are per-load copies (tasks may keep per-run state) — don't leak one per transition.
                    foreach (var taskAsset in instancedPreloadTasks)
                    {
                        if (taskAsset) Destroy(taskAsset);
                    }
                }
            }
            else
            {
                progress.Report(1f);
            }

            await progress.CompleteAsync();
        }

        #endregion

        #region Helpers

        private void NotifyTransitionCompleted(TransitionKind kind, IScreenDefinition from, IScreenDefinition to, TransitionContext context)
        {
            // Lambda (not the delegate value) so subscribers added during the MessagePipe publish are still invoked, as before.
            EventDispatcher.NotifyTransitionCompleted(kind, from, to, context, args => OnTransitionCompleted?.Invoke(args));
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
                    if (t != null) t.RegisterListener(listener);
                }
            }

            public void UnregisterListener(IScreenEventListener listener)
            {
                if (_profiles == null) return;
                foreach (var t in _profiles)
                {
                    if (t != null) t.UnregisterListener(listener);
                }
            }
        }

        #endregion
    }
}
