using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Events.Global;
using _VuTH.Core.GameCycle.Screen.Loading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.GameCycle.Screen.Tests.PlayMode
{
    public class ScreenSafetyPlayModeTests
    {
        private readonly List<Object> _objects = new();
        private readonly List<Scene> _scenes = new();
        private readonly List<Dictionary<string, SceneInstance>> _caches = new();
        private float _timeScale;

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }
        private ScreenModel Screen(string name)
        {
            var screen = Track(ScriptableObject.CreateInstance<ScreenModel>());
            screen.name = name;
            screen.showLoadingScreen = false;
            return screen;
        }

        [SetUp] public void SetUp() => _timeScale = Time.timeScale;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _timeScale;
            // Test scenes are created by SceneManager, not Addressables: clean them up with their actual owner.
            foreach (var cache in _caches) cache.Clear();
            foreach (var obj in _objects) if (obj) Object.DestroyImmediate(obj);
            foreach (var scene in _scenes)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            _objects.Clear(); _scenes.Clear(); _caches.Clear();
        }

        private (DefaultSliderLoadingController controller, GameObject root, CanvasGroup group) Loading(bool separateRoot, bool withGroup = true)
        {
            var host = Track(new GameObject("Loading host"));
            host.SetActive(false);
            var root = separateRoot ? Track(new GameObject("Loading root")) : host;
            var visual = Track(new GameObject("Loading visual"));
            visual.transform.SetParent(root.transform);
            var group = withGroup ? visual.AddComponent<CanvasGroup>() : null;
            var controller = host.AddComponent<DefaultSliderLoadingController>();
            Set(controller, "root", root);
            Set(controller, "canvasGroup", group);
            Set(controller, "animationDuration", 0.05f);
            host.SetActive(true); // Awake hides the configured UI.
            return (controller, root, group);
        }

        private static IEnumerator Completes(Task task)
        {
            var deadline = Time.realtimeSinceStartup + 2f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, "Loading animation stalled while paused");
            task.GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator Loading_ShowAndHideCompleteWhenPaused()
        {
            var ui = Loading(false);
            Time.timeScale = 0;
            yield return Completes(ui.controller.Show().AsTask());
            Assert.AreEqual(1f, ui.group.alpha);
            yield return Completes(ui.controller.Hide().AsTask());
            Assert.IsFalse(ui.root.activeSelf);
        }

        [UnityTest]
        public IEnumerator Loading_RestoresSeparateRootAndInputAcrossRepeatedShows()
        {
            var ui = Loading(true);
            for (var i = 0; i < 2; i++)
            {
                yield return Completes(ui.controller.Show().AsTask());
                Assert.IsTrue(ui.root.activeInHierarchy);
                Assert.IsTrue(ui.group.gameObject.activeInHierarchy);
                Assert.IsTrue(ui.group.blocksRaycasts);
                Assert.IsTrue(ui.group.interactable);
                yield return Completes(ui.controller.Hide().AsTask());
                Assert.IsFalse(ui.root.activeSelf);
                Assert.IsFalse(ui.group.blocksRaycasts);
            }
        }

        [UnityTest]
        public IEnumerator Loading_WithoutCanvasGroupStillShowsAndHidesRoot()
        {
            var ui = Loading(true, false);
            yield return ui.controller.Show().ToCoroutine();
            Assert.IsTrue(ui.root.activeSelf);
            yield return ui.controller.Hide().ToCoroutine();
            Assert.IsFalse(ui.root.activeSelf);
        }

        private ScreenManager Manager()
        {
            var host = Track(new GameObject("Screen manager test"));
            host.SetActive(false); // Configure navigation directly; no bootstrap/container or external scenes needed.
            return host.AddComponent<ScreenManager>();
        }

        private (AddressableSceneCache cache, Scene scene, GameObject active, GameObject inactive) CacheScene(ScreenManager manager, AssetReference reference,
            IScreenSceneOperations operations = null)
        {
            if (operations != null)
                Set(manager, "_sceneCache", new AddressableSceneCache(manager,
                    () => Get<ScreenNavigationState>(manager, "_nav").OwnedScreens, operations));
            var cache = (AddressableSceneCache)typeof(ScreenManager).GetProperty("SceneCache", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            var scene = SceneManager.CreateScene("ScreenSafety_" + Guid.NewGuid().ToString("N"));
            _scenes.Add(scene);
            var active = new GameObject("Designed active");
            var inactive = new GameObject("Designed inactive");
            inactive.SetActive(false);
            SceneManager.MoveGameObjectToScene(active, scene);
            SceneManager.MoveGameObjectToScene(inactive, scene);
            object instance = new SceneInstance();
            typeof(SceneInstance).GetProperty("Scene").SetValue(instance, scene);
            var cached = Get<Dictionary<string, SceneInstance>>(cache, "_cachedAddressableScenes");
            cached.Add(reference.AssetGUID, (SceneInstance)instance);
            _caches.Add(cached);
            return (cache, scene, active, inactive);
        }

        [Test]
        public void SceneShowAndRepeatedHide_PreserveRootActiveStates()
        {
            var screen = Screen("A");
            screen.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var cached = CacheScene(Manager(), screen.mainSceneRef);
            cached.cache.ShowScreen(screen);
            Assert.IsFalse(cached.inactive.activeSelf, "First show must preserve authored inactive roots");
            cached.cache.HideScreen(screen, null);
            cached.cache.HideScreen(screen, null);
            Assert.IsFalse(cached.active.activeSelf);
            cached.cache.ShowScreen(screen);
            Assert.IsTrue(cached.active.activeSelf);
            Assert.IsFalse(cached.inactive.activeSelf);
            cached.active.SetActive(false);
            cached.cache.HideScreen(screen, null);
            cached.cache.ShowScreen(screen);
            Assert.IsFalse(cached.active.activeSelf, "Runtime state before hide must also survive");
        }

        [UnityTest]
        public IEnumerator Pop_KeepsSharedMainSceneOwnedDeeperInStack() => UniTask.ToCoroutine(async () =>
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B"); var c = Screen("C");
            a.mainSceneRef = c.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var cached = CacheScene(manager, a.mainSceneRef);
            nav.Base = a; nav.Push(a); nav.Push(b); nav.Push(c);
            await manager.Pop();
            Assert.AreEqual(cached.scene, cached.cache.GetLoadedScene(a.mainSceneRef));
            Assert.AreSame(b, manager.Current);
            Assert.IsTrue(cached.active.activeSelf, "An owner that was never hidden must stay visible");
            await manager.Pop();
            Assert.AreSame(a, manager.Current);
            Assert.IsTrue(cached.active.activeSelf);
            Assert.IsFalse(cached.inactive.activeSelf);
        });

        [UnityTest]
        public IEnumerator Pop_SharedSceneReturnsToHiddenOwnerUntilThatOwnerIsShown() => UniTask.ToCoroutine(async () =>
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B"); var c = Screen("C");
            a.mainSceneRef = c.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var cached = CacheScene(manager, a.mainSceneRef);
            nav.Base = a; nav.Push(a);
            await manager.Push(b, true, _VuTH.Core.GameCycle.Screen.Transition.TransitionContext.Default);
            Assert.IsFalse(cached.active.activeSelf);
            await manager.Push(c);
            Assert.IsTrue(cached.active.activeSelf);
            await manager.Pop();
            Assert.AreEqual(cached.scene, cached.cache.GetLoadedScene(a.mainSceneRef));
            Assert.IsFalse(cached.active.activeSelf);
            await manager.Pop();
            Assert.IsTrue(cached.active.activeSelf);
            Assert.IsFalse(cached.inactive.activeSelf);
        });

        private sealed class ThrowBeforeExit : IScreenEventListener
        {
            public void OnPreScreenExit(ScreenEventArgs args) => throw new InvalidOperationException("pre-exit failed");
            public void OnPostScreenExit(ScreenEventArgs args) { }
            public void OnPreScreenEnter(ScreenEventArgs args) { }
            public void OnPostScreenEnter(ScreenEventArgs args) { }
        }

        private sealed class FaultingSceneOperations : IScreenSceneOperations
        {
            public Scene FailUnload;
            public readonly Dictionary<string, SceneInstance> Loads = new();
            public UniTask<SceneInstance> LoadAsync(AssetReference reference) =>
                Loads.TryGetValue(reference.AssetGUID, out var instance) ? UniTask.FromResult(instance) :
                    UniTask.FromException<SceneInstance>(new InvalidOperationException("load failed"));
            public async UniTask UnloadAsync(SceneInstance instance)
            {
                if (instance.Scene == FailUnload) throw new InvalidOperationException("additive unload failed");
                if (instance.Scene.isLoaded) await SceneManager.UnloadSceneAsync(instance.Scene).ToUniTask();
            }
        }

        [UnityTest] public IEnumerator Pop_PartialUnloadFailure() => PartialUnloadFailure("Pop").ToCoroutine();
        [UnityTest] public IEnumerator PopOverride_PartialUnloadFailure() => PartialUnloadFailure("PopOverride").ToCoroutine();
        [UnityTest] public IEnumerator Enter_PartialUnloadFailure() => PartialUnloadFailure("Enter").ToCoroutine();
        [UnityTest] public IEnumerator Enter_PartialUnloadFailureLoadsUncachedDestination() => PartialUnloadFailure("Enter", true).ToCoroutine();
        [UnityTest] public IEnumerator ReplaceOverride_PartialUnloadFailure() => PartialUnloadFailure("ReplaceOverride").ToCoroutine();

        private async UniTask PartialUnloadFailure(string operation, bool uncachedDestination = false)
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var destination = Screen("Destination"); var closing = Screen("Closing");
            destination.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            closing.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var additive = new AssetReference(Guid.NewGuid().ToString("N"));
            closing.additiveScene = new[] { new AdditiveSceneAddressableData { sceneRef = additive, unloadOnClose = true } };
            var operations = new FaultingSceneOperations();
            var target = CacheScene(manager, destination.mainSceneRef, operations);
            var main = CacheScene(manager, closing.mainSceneRef);
            var extra = CacheScene(manager, additive);
            operations.FailUnload = extra.scene;
            if (operation == "Enter") { nav.Base = closing; nav.Push(closing); }
            else
            {
                nav.Base = destination; nav.Push(destination);
                if (operation == "Pop") nav.Push(closing); else nav.Override = closing;
            }
            target.cache.HideScreen(destination, closing);
            if (uncachedDestination)
            {
                var cache = Get<Dictionary<string, SceneInstance>>(target.cache, "_cachedAddressableScenes");
                operations.Loads[destination.mainSceneRef.AssetGUID] = cache[destination.mainSceneRef.AssetGUID];
                cache.Remove(destination.mainSceneRef.AssetGUID);
            }
            var completed = 0;
            manager.OnTransitionCompleted += _ => completed++;
            if (operation == "Enter")
            {
                if (uncachedDestination && typeof(AddressableSceneCache).GetProperty("CoreLifetimeScope") != null)
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Core LifetimeScope is null"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Enter.*failed"));
            }
            InvalidOperationException error = null;
            try
            {
                switch (operation)
                {
                    case "Pop": await manager.Pop(); break;
                    case "PopOverride": await manager.PopOverride(); break;
                    case "Enter": await manager.Enter(destination); break;
                    default: await manager.PushOverride(Screen("Replacement")); break;
                }
            }
            catch (InvalidOperationException ex) { error = ex; }
            Assert.IsNotNull(error, "The unload error must reach the navigation caller");
            Assert.AreEqual("additive unload failed", error.Message);
            Assert.IsFalse(main.scene.isLoaded, "The main scene really unloaded before the additive failure");
            Assert.AreSame(destination, manager.Current);
            Assert.IsFalse(manager.IsOverriding);
            Assert.IsTrue(target.active.activeSelf);
            Assert.IsFalse(target.inactive.activeSelf);
            Assert.IsFalse(manager.IsTransitioning);
            Assert.AreEqual(0, completed);
        }

        [Test]
        public void InvalidSceneReference_ThrowsInsteadOfReportingSuccess()
        {
            var cache = new AddressableSceneCache(this);
            Assert.ThrowsAsync<ArgumentException>(async () => await cache.LoadOrShowSceneAsync(new AssetReference("")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DuplicatePush_IsRejectedBeforeChangingVisibility(bool asOverride)
        {
            var manager = Manager(); var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B");
            nav.Base = a; nav.Push(a); nav.Push(b);
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                if (asOverride) await manager.PushOverride(a); else await manager.Push(a);
            });
            Assert.AreSame(b, manager.Current);
            Assert.AreEqual(2, nav.StackCount);
        }

        [Test]
        public void Rehide_DisablesRootsReactivatedWhileHiddenWithoutReplacingSnapshot()
        {
            var screen = Screen("Hidden"); screen.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var cached = CacheScene(Manager(), screen.mainSceneRef);
            cached.cache.HideScreen(screen, null);
            cached.active.SetActive(true); cached.inactive.SetActive(true);
            cached.cache.HideScreen(screen, null);
            Assert.IsFalse(cached.active.activeSelf); Assert.IsFalse(cached.inactive.activeSelf);
            cached.cache.ShowScreen(screen);
            Assert.IsTrue(cached.active.activeSelf); Assert.IsFalse(cached.inactive.activeSelf);
        }

        [UnityTest]
        public IEnumerator InterruptedFade_ContinuesFromCurrentAlphaAndDisablesVisualWhenHidden()
        {
            var ui = Loading(true);
            Set(ui.controller, "animationDuration", 0.2f);
            yield return Completes(ui.controller.Show().AsTask());
            var hide = ui.controller.Hide().AsTask();
            yield return new WaitForSecondsRealtime(0.08f);
            var alpha = ui.group.alpha;
            var show = ui.controller.Show().AsTask();
            Assert.AreEqual(alpha, ui.group.alpha, 0.01f, "Interrupting fade-out must not restart at zero");
            yield return Completes(show);
            yield return Completes(hide);
            yield return Completes(ui.controller.Hide().AsTask());
            Assert.IsFalse(ui.group.gameObject.activeSelf);
        }

        private sealed class ThrowAtPhase : IScreenEventListener
        {
            public string Phase;
            public int PostEnterCount;
            private void Notify(string phase)
            {
                if (Phase == phase) throw new InvalidOperationException("failed at " + phase);
            }
            public void OnPreScreenExit(ScreenEventArgs args) => Notify("PreExit");
            public void OnPostScreenExit(ScreenEventArgs args) => Notify("PostExit");
            public void OnPreScreenEnter(ScreenEventArgs args) => Notify("PreEnter");
            public void OnPostScreenEnter(ScreenEventArgs args) { PostEnterCount++; Notify("PostEnter"); }
        }

        [UnityTest]
        public IEnumerator FailedSceneLoad_DoesNotPublishSuccessfulEnterOrCompleteNavigation() => UniTask.ToCoroutine(async () =>
        {
            var manager = Manager(); var nav = Get<ScreenNavigationState>(manager, "_nav");
            var from = Screen("Visible"); var target = Screen("Fails loading");
            from.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            target.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var cached = CacheScene(manager, from.mainSceneRef, new FaultingSceneOperations());
            nav.Base = from; nav.Push(from);
            var listener = new ThrowAtPhase();
            var profile = Track(ScriptableObject.CreateInstance<GlobalScreenEventProfile>());
            profile.RegisterListener(listener);
            Set(manager, "eventProfile", new[] { profile });
            var completed = 0;
            manager.OnTransitionCompleted += _ => completed++;
            if (typeof(AddressableSceneCache).GetProperty("CoreLifetimeScope") != null)
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Core LifetimeScope is null"));
            InvalidOperationException failure = null;
            try { await manager.Push(target); }
            catch (InvalidOperationException error) { failure = error; }
            Assert.IsNotNull(failure);
            Assert.AreEqual("load failed", failure.Message);
            Assert.AreEqual(0, listener.PostEnterCount);
            Assert.AreEqual(0, completed);
            Assert.AreSame(from, manager.Current);
            Assert.IsTrue(cached.active.activeSelf);
            Assert.IsFalse(manager.IsTransitioning);
        });

        private sealed class FailingLoadingController : ILoadingController
        {
            public UniTask Show() => UniTask.FromException(new InvalidOperationException("loading failed"));
            public UniTask Hide() => UniTask.CompletedTask;
            public void SetVisible(bool visible) { }
            public void SetProgress(float value) { }
        }

        [Test]
        public void Enter_LoadingShowFailureKeepsExistingNavigation()
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B"); var target = Screen("Target");
            target.showLoadingScreen = true;
            nav.Base = a; nav.Push(a); nav.Push(b); nav.Previous = a;
            Set(manager, "_loading", new FailingLoadingController());
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Enter.*failed"));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.Enter(target));
            Assert.AreSame(b, manager.Current);
            Assert.AreSame(a, manager.Previous);
            Assert.AreSame(a, nav.Base);
            Assert.AreEqual(2, nav.StackCount);
            Assert.IsFalse(manager.IsTransitioning);
        }

        [TestCase("PostExit")]
        [TestCase("PreEnter")]
        [TestCase("PostEnter")]
        public void Enter_CallbackFailureAfterCloseKeepsTargetNavigation(string phase)
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B"); var target = Screen("Target");
            b.softCache = true;
            b.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var cached = CacheScene(manager, b.mainSceneRef);
            nav.Base = a; nav.Push(a); nav.Push(b);
            var profile = Track(ScriptableObject.CreateInstance<GlobalScreenEventProfile>());
            profile.RegisterListener(new ThrowAtPhase { Phase = phase });
            Set(manager, "eventProfile", new[] { profile });
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Enter.*failed"));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.Enter(target));
            Assert.AreSame(target, manager.Current);
            Assert.AreSame(target, nav.Base);
            Assert.AreEqual(1, nav.StackCount);
            Assert.IsFalse(manager.CanPop);
            Assert.IsFalse(cached.active.activeSelf);
            Assert.IsFalse(manager.IsTransitioning);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Hide_SharedSceneStaysVisibleForAnotherStackOwner(bool additive)
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B"); var c = Screen("C");
            var shared = new AssetReference(Guid.NewGuid().ToString("N"));
            if (additive)
                a.additiveScene = b.additiveScene = new[] { new AdditiveSceneAddressableData { sceneRef = shared } };
            else a.mainSceneRef = b.mainSceneRef = shared;
            var cached = CacheScene(manager, shared);
            nav.Base = a; nav.Push(a);
            await manager.Push(b);
            await manager.Push(c, true, _VuTH.Core.GameCycle.Screen.Transition.TransitionContext.Default);
            Assert.IsTrue(cached.active.activeSelf, "A is still visible and owns the scene hidden by B");
            Assert.IsFalse(cached.inactive.activeSelf);
            cached.cache.HideScreen(a, c);
            Assert.IsFalse(cached.active.activeSelf, "The scene may hide once every owner is hidden");
        }

        [TestCase("PreExit")]
        [TestCase("PostExit")]
        [TestCase("PreEnter")]
        [TestCase("PostEnter")]
        public async Task Pop_CallbackFailureKeepsStateConsistentWithClosurePhase(string phase)
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B");
            b.softCache = true; // Closing really hides its roots, without requiring an Addressables catalog.
            b.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var cached = CacheScene(manager, b.mainSceneRef);
            nav.Base = a; nav.Push(a); nav.Push(b);
            var profile = Track(ScriptableObject.CreateInstance<GlobalScreenEventProfile>());
            var listener = new ThrowAtPhase { Phase = phase };
            profile.RegisterListener(listener);
            Set(manager, "eventProfile", new[] { profile });
            Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.Pop());
            var beforeClose = phase == "PreExit";
            Assert.AreSame(beforeClose ? b : a, manager.Current);
            Assert.AreEqual(beforeClose, cached.active.activeSelf);
            Assert.AreEqual(beforeClose, manager.CanPop);
            Assert.IsFalse(manager.IsTransitioning);
            profile.UnregisterListener(listener);
            if (beforeClose) await manager.Pop();
            Assert.AreSame(a, manager.Current);
        }

        [Test]
        public async Task Enter_PreExitFailurePreservesStackOverrideAndPrevious()
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B"); var overlay = Screen("Override"); var target = Screen("Target");
            nav.Base = a; nav.Push(a); nav.Push(b); nav.Override = overlay; nav.Previous = a;
            var profile = Track(ScriptableObject.CreateInstance<GlobalScreenEventProfile>());
            var listener = new ThrowBeforeExit();
            profile.RegisterListener(listener);
            Set(manager, "eventProfile", new[] { profile });
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Enter.*failed"));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.Enter(target));
            Assert.AreSame(overlay, manager.Current);
            Assert.AreSame(a, manager.Previous);
            Assert.AreSame(a, nav.Base);
            Assert.AreSame(b, nav.Peek());
            Assert.AreEqual(2, nav.StackCount);
            Assert.IsTrue(manager.IsOverriding);
            Assert.IsFalse(manager.IsTransitioning);
            profile.UnregisterListener(listener);
            await manager.PopOverride();
            await manager.Pop();
            Assert.AreSame(a, manager.Current);
        }

        [UnityTest]
        public IEnumerator ReplaceOverride_KeepsSharedAdditiveSceneOwnedByUnderlyingScreen() => UniTask.ToCoroutine(async () =>
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var b = Screen("B");
            var oldOverride = Screen("Old override"); var nextOverride = Screen("New override");
            var shared = new AssetReference(Guid.NewGuid().ToString("N"));
            a.additiveScene = oldOverride.additiveScene = new[]
                { new AdditiveSceneAddressableData { sceneRef = shared, unloadOnClose = true } };
            var cached = CacheScene(manager, shared);
            nav.Base = a; nav.Push(a); nav.Push(b); nav.Override = oldOverride;
            await manager.PushOverride(nextOverride);
            Assert.AreEqual(cached.scene, cached.cache.GetLoadedScene(shared));
            await manager.PopOverride();
            await manager.Pop();
            Assert.AreSame(a, manager.Current);
            Assert.IsTrue(cached.active.activeSelf);
        });

        [UnityTest]
        public IEnumerator ClosingSoftCachedScreen_DoesNotHideSceneSharedWithNextScreen() => UniTask.ToCoroutine(async () =>
        {
            var a = Screen("A"); var b = Screen("B");
            a.softCache = true;
            a.mainSceneRef = b.mainSceneRef = new AssetReference(Guid.NewGuid().ToString("N"));
            var cached = CacheScene(Manager(), a.mainSceneRef);
            await cached.cache.UnloadScreenAsync(a, b);
            Assert.IsTrue(cached.active.activeSelf);
            Assert.IsFalse(cached.inactive.activeSelf);
        });

        [UnityTest]
        public IEnumerator PopOverride_PreExitFailurePreservesOverrideAndAllowsRetry() => UniTask.ToCoroutine(async () =>
        {
            var manager = Manager();
            var nav = Get<ScreenNavigationState>(manager, "_nav");
            var a = Screen("A"); var overlay = Screen("Override");
            nav.Base = a; nav.Push(a); nav.Override = overlay;
            var profile = Track(ScriptableObject.CreateInstance<GlobalScreenEventProfile>());
            var listener = new ThrowBeforeExit();
            profile.RegisterListener(listener);
            Set(manager, "eventProfile", new[] { profile });
            Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.PopOverride());
            Assert.IsTrue(manager.IsOverriding);
            Assert.AreSame(overlay, manager.Current);
            Assert.IsFalse(manager.IsTransitioning);
            profile.UnregisterListener(listener);
            await manager.PopOverride();
            Assert.IsFalse(manager.IsOverriding);
            Assert.AreSame(a, manager.Current);
        });
    }
}
