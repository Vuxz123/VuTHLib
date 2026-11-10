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

        private (AddressableSceneCache cache, Scene scene, GameObject active, GameObject inactive) CacheScene(ScreenManager manager, AssetReference reference)
        {
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

        private sealed class ThrowAtPhase : IScreenEventListener
        {
            public string Phase;
            private void Notify(string phase)
            {
                if (Phase == phase) throw new InvalidOperationException("failed at " + phase);
            }
            public void OnPreScreenExit(ScreenEventArgs args) => Notify("PreExit");
            public void OnPostScreenExit(ScreenEventArgs args) => Notify("PostExit");
            public void OnPreScreenEnter(ScreenEventArgs args) => Notify("PreEnter");
            public void OnPostScreenEnter(ScreenEventArgs args) => Notify("PostEnter");
        }

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
