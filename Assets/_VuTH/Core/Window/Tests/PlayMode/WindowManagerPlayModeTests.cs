using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using _VuTH.Core.Pool;
using _VuTH.Core.Window.Blocker;
using _VuTH.Core.Window.Transition;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Window.Tests.PlayMode
{
    /// <summary>
    /// The window flow end to end with a real pool: input blocking, results, failures, destroyed windows, CloseAll,
    /// back press, cancellation, paused game and reuse of pooled windows.
    /// </summary>
    public class WindowManagerPlayModeTests
    {
        public class TestWindow : PopupBase
        {
            public int Shown, Hidden, Back;

            public override void Setup(object data)
            {
                if (data as string == "throw") throw new InvalidOperationException("setup failed");
            }

            public override void OnViewShown() => Shown++;
            public override void OnViewHidden() => Hidden++;

            public override void OnBackPressed()
            {
                Back++;
                base.OnBackPressed();
            }

            public void CloseWith(object result) => Close(result);
        }

        public class NoBackWindow : TestWindow
        {
            public override bool AllowBackClose => false;
        }

        private sealed class PrefabProvider : IWindowPrefabProvider
        {
            public readonly Dictionary<Type, GameObject> Prefabs = new();
            public UniTask<GameObject> GetOrLoad(Type windowType) => UniTask.FromResult(Prefabs[windowType]);
            public bool TryGetLoaded(Type windowType, out GameObject prefab) => Prefabs.TryGetValue(windowType, out prefab);
            public readonly List<Type> Released = new();
            public void Release(Type windowType) => Released.Add(windowType);
            public void Clear() { }
        }

        private sealed class RecordingBlocker : IUIInputBlocker
        {
            public int Count;
            public bool IsBlocked => Count > 0;
            public void Block(string reason = null) => Count++;
            public void Unblock(string reason = null) => Count--;
        }

        /// <summary>Transitions that wait for the test, and record whether input was blocked meanwhile.</summary>
        private sealed class GatedFactory : IUITransitionFactory
        {
            public RecordingBlocker Blocker;
            public UniTaskCompletionSource Gate;
            public bool Running, BlockedWhileRunning;

            public IUITransition Create(UITransitionSettings settings) => new Gated(this);
            public IUITransition Create(string presetName) => new Gated(this);

            private sealed class Gated : IUITransition
            {
                private readonly GatedFactory _factory;
                public Gated(GatedFactory factory) => _factory = factory;
                public float Duration => 0f;

                public UniTask In(IUIView view) => Run(view, true);
                public UniTask Out(IUIView view) => Run(view, false);

                private async UniTask Run(IUIView view, bool visible)
                {
                    _factory.Running = true;
                    _factory.BlockedWhileRunning = _factory.Blocker.IsBlocked;
                    if (_factory.Gate != null) await _factory.Gate.Task;
                    _factory.Running = false;

                    view.CanvasGroup.alpha = visible ? 1f : 0f;
                    view.CanvasGroup.interactable = visible;
                    view.CanvasGroup.blocksRaycasts = visible;
                }
            }
        }

        private readonly List<GameObject> _owned = new();
        private WindowManager _manager;
        private RecordingBlocker _blocker;
        private PrefabProvider _prefabs;
        private PoolManager _pool;

        [SetUp]
        public void SetUp()
        {
            var root = new GameObject("PoolRoot_Test").transform;
            _owned.Add(root.gameObject);

            var poolGo = new GameObject("PoolManager_Test");
            poolGo.SetActive(false);
            _owned.Add(poolGo);
            var pool = _pool = poolGo.AddComponent<PoolManager>();
            SetField(pool, "rootPoolTransform", root);
            SetField(pool, "warmupOnAwake", false);
            poolGo.SetActive(true);

            _prefabs = new PrefabProvider();
            _prefabs.Prefabs[typeof(TestWindow)] = CreateTemplate<TestWindow>();
            _prefabs.Prefabs[typeof(NoBackWindow)] = CreateTemplate<NoBackWindow>();

            var managerGo = new GameObject("WindowManager_Test");
            _owned.Add(managerGo);
            _blocker = new RecordingBlocker();
            _manager = managerGo.AddComponent<WindowManager>()
                .WithPrefabProvider(_prefabs)
                .WithInputBlocker(_blocker)
                .WithTransitionFactory(null);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_manager) Object.DestroyImmediate(_manager.gameObject);
            foreach (var go in _owned)
            {
                if (go) Object.DestroyImmediate(go);
            }

            _owned.Clear();
            foreach (var window in Object.FindObjectsByType<TestWindow>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(window.gameObject);
            }
        }

        private GameObject CreateTemplate<T>() where T : UIViewBase
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.SetActive(false);
            go.AddComponent<T>();
            _owned.Add(go);
            return go;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private static async UniTask WaitUntil(Func<bool> condition, string what)
        {
            var timeout = Time.realtimeSinceStartup + 3f;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > timeout) Assert.Fail($"Timed out waiting for: {what}");
                await UniTask.Yield();
            }
        }

        // ---- Input blocking ----

        [UnityTest]
        public IEnumerator Input_IsBlockedOnlyWhileTransitioning() => UniTask.ToCoroutine(async () =>
        {
            // It used to stay blocked for as long as the window was open, so its own buttons could not be clicked.
            var factory = new GatedFactory { Blocker = _blocker, Gate = new UniTaskCompletionSource() };
            _manager.WithTransitionFactory(factory);

            var showing = _manager.Show<TestWindow>();
            await WaitUntil(() => factory.Running, "transition in");
            Assert.IsTrue(factory.BlockedWhileRunning);
            Assert.IsTrue(_manager.IsTransitioning);

            factory.Gate.TrySetResult();
            var window = await showing;
            Assert.IsFalse(_blocker.IsBlocked, "open window: input is free");
            Assert.IsFalse(_manager.IsTransitioning);
            Assert.IsTrue(window.CanvasGroup.interactable);

            factory.Gate = new UniTaskCompletionSource();
            var closing = _manager.Close(window);
            await WaitUntil(() => factory.Running, "transition out");
            Assert.IsTrue(factory.BlockedWhileRunning);

            factory.Gate.TrySetResult();
            await closing;
            Assert.IsFalse(_blocker.IsBlocked);
            Assert.AreEqual(0, _manager.WindowCount);
        });

        // ---- Results ----

        [UnityTest]
        public IEnumerator Open_CompletesOnClose_WithTheResult() => UniTask.ToCoroutine(async () =>
        {
            var opening = _manager.Open<TestWindow, int>();
            await WaitUntil(() => _manager.TopWindow, "shown");
            var window = (TestWindow)_manager.TopWindow;
            Assert.AreEqual(UniTaskStatus.Pending, opening.Status, "Open completes when the window closes");
            Assert.AreEqual(1, window.Shown);

            window.CloseWith(7);

            Assert.AreEqual(7, await opening);
            Assert.AreEqual(1, window.Hidden);
            Assert.AreEqual(0, _manager.WindowCount);
        });

        [UnityTest]
        public IEnumerator Open_ResultOfAnotherType_ReturnsDefault() => UniTask.ToCoroutine(async () =>
        {
            var opening = _manager.Open<TestWindow, int>();
            await WaitUntil(() => _manager.TopWindow, "shown");
            LogAssert.Expect(LogType.Error, new Regex("was expected"));

            ((TestWindow)_manager.TopWindow).CloseWith("text");

            Assert.AreEqual(0, await opening);
        });

        // ---- Failures ----

        [UnityTest]
        public IEnumerator SetupThrows_TheWindowIsRemoved_AndTheManagerKeepsWorking() => UniTask.ToCoroutine(async () =>
        {
            // Used to leave the window in the stack and input blocked for good.
            var threw = false;
            try
            {
                await _manager.Show<TestWindow>("throw");
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw);
            Assert.AreEqual(0, _manager.WindowCount);
            Assert.IsFalse(_manager.IsTransitioning);
            Assert.IsFalse(_blocker.IsBlocked);

            var window = await _manager.Show<TestWindow>();
            Assert.IsNotNull(window);
            await _manager.Close(window);
        });

        [UnityTest]
        public IEnumerator DestroyedWindow_IsRemoved_AndReleasesItsCaller() => UniTask.ToCoroutine(async () =>
        {
            var opening = _manager.Open<TestWindow, int>();
            await WaitUntil(() => _manager.TopWindow, "shown");

            Object.Destroy(_manager.TopWindow.gameObject);
            await UniTask.NextFrame();

            Assert.AreEqual(0, await opening);
            Assert.AreEqual(0, _manager.WindowCount);
        });

        // ---- CloseAll ----

        [UnityTest]
        public IEnumerator CloseAll_ForceCleanup_ClosesEachWindowOnce_AndPooledWindowsWorkAgain() => UniTask.ToCoroutine(async () =>
        {
            var closedEvents = 0;
            _manager.OnWindowClosed += _ => closedEvents++;
            var a = await _manager.Show<TestWindow>();
            var b = await _manager.Show<TestWindow>();

            await _manager.CloseAll(forceCleanup: true);
            await UniTask.NextFrame();

            Assert.AreEqual(0, _manager.WindowCount);
            Assert.AreEqual(2, closedEvents, "each window closed once (it used to be closed twice)");
            Assert.AreEqual(1, a.Hidden);
            Assert.AreEqual(1, b.Hidden);

            // A pooled instance used to stay on screen after its next close.
            var again = await _manager.Show<TestWindow>();
            await _manager.Close(again);
            Assert.AreEqual(0, _manager.WindowCount);
            Assert.IsFalse(again.gameObject.activeInHierarchy, "returned to the pool");
        });

        [UnityTest]
        public IEnumerator CloseAll_ReleasesOpenCallersWithDefault() => UniTask.ToCoroutine(async () =>
        {
            var first = _manager.Open<TestWindow, int>();
            var second = _manager.Open<TestWindow, int>();
            await WaitUntil(() => _manager.WindowCount == 2, "both shown");

            await _manager.CloseAll();

            Assert.AreEqual(0, await first);
            Assert.AreEqual(0, await second);
            Assert.AreEqual(0, _manager.WindowCount);
        });

        // ---- Back press ----

        [UnityTest]
        public IEnumerator BackPress_GoesThroughOnBackPressed() => UniTask.ToCoroutine(async () =>
        {
            // The manager used to close the window itself, bypassing OnBackPressed overrides.
            var window = await _manager.Show<TestWindow>();

            Assert.IsTrue(await _manager.HandleBackPressAsync());
            await WaitUntil(() => _manager.WindowCount == 0, "closed");
            Assert.AreEqual(1, window.Back);
        });

        [UnityTest]
        public IEnumerator BackPress_IgnoredByAWindowThatOptsOut() => UniTask.ToCoroutine(async () =>
        {
            var window = await _manager.Show<NoBackWindow>();

            Assert.IsFalse(await _manager.HandleBackPressAsync());
            Assert.AreEqual(0, window.Back);
            Assert.AreEqual(1, _manager.WindowCount);
        });

        // ---- Cancellation ----

        [UnityTest]
        public IEnumerator Cancel_ClosesTheWindow_AndThrows() => UniTask.ToCoroutine(async () =>
        {
            using var cts = new CancellationTokenSource();
            var opening = _manager.Open<TestWindow, int>(null, cts.Token);
            await WaitUntil(() => _manager.TopWindow, "shown");

            cts.Cancel();

            var canceled = false;
            try
            {
                await opening;
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            Assert.IsTrue(canceled);
            Assert.AreEqual(0, _manager.WindowCount);
        });

        // ---- Real transitions ----

        [UnityTest]
        public IEnumerator Transitions_RunWhileTheGameIsPaused() => UniTask.ToCoroutine(async () =>
        {
            // Scaled-time tweens never finished at timeScale 0 and every later window operation hung.
            var factoryGo = new GameObject("TransitionFactory_Test");
            _owned.Add(factoryGo);
            _manager.WithTransitionFactory(factoryGo.AddComponent<UITransitionFactory>());
            Time.timeScale = 0f;

            var window = await _manager.Show<TestWindow>(new WindowOptions
            {
                TransitionInSettings = new FadeTransitionSettings { duration = 0.05f },
                TransitionOutSettings = new FadeTransitionSettings { duration = 0.05f },
            }).Timeout(TimeSpan.FromSeconds(3), DelayType.Realtime);

            Assert.AreEqual(1f, window.CanvasGroup.alpha);
            await _manager.Close(window).Timeout(TimeSpan.FromSeconds(3), DelayType.Realtime);
        });

        [UnityTest]
        public IEnumerator PooledWindow_ReopensInPlace_AfterASlideOut() => UniTask.ToCoroutine(async () =>
        {
            // The slide in used to end where the previous slide out left the window: off screen.
            var factoryGo = new GameObject("TransitionFactory_Test");
            _owned.Add(factoryGo);
            _manager.WithTransitionFactory(factoryGo.AddComponent<UITransitionFactory>());
            var options = new WindowOptions
            {
                TransitionInSettings = new SlideTransitionSettings { duration = 0.05f },
                TransitionOutSettings = new SlideTransitionSettings { duration = 0.05f },
            };

            var first = await _manager.Show<TestWindow>(options);
            var rest = ((RectTransform)first.transform).anchoredPosition;
            await _manager.Close(first);

            var second = await _manager.Show<TestWindow>(options);

            Assert.AreSame(first, second, "the pooled instance is reused");
            Assert.AreEqual(rest, ((RectTransform)second.transform).anchoredPosition);
        });

        // ---- Duplicates ----

        private static WindowOptions Duplicate(WindowDuplicatePolicy policy) => new() { Duplicate = policy };

        [UnityTest]
        public IEnumerator Duplicate_Ignore_SharesTheOpenWindow_AndItsResult() => UniTask.ToCoroutine(async () =>
        {
            var owner = _manager.Open<TestWindow, int>();
            await WaitUntil(() => _manager.TopWindow, "shown");
            var first = (TestWindow)_manager.TopWindow;

            var second = await _manager.Show<TestWindow>(Duplicate(WindowDuplicatePolicy.Ignore));
            var sharing = _manager.Open<TestWindow, int>(Duplicate(WindowDuplicatePolicy.Ignore));
            await UniTask.NextFrame();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, _manager.WindowCount);

            first.CloseWith(4);
            Assert.AreEqual(4, await owner);
            Assert.AreEqual(4, await sharing);
        });

        [UnityTest]
        public IEnumerator Duplicate_BringToFront_MovesTheOpenWindowOnTop() => UniTask.ToCoroutine(async () =>
        {
            var bottom = await _manager.Show<TestWindow>();
            var top = await _manager.Show<NoBackWindow>();

            var again = await _manager.Show<TestWindow>(Duplicate(WindowDuplicatePolicy.BringToFront));

            Assert.AreSame(bottom, again);
            Assert.AreSame(bottom, _manager.TopWindow);
            Assert.Greater(bottom.Canvas.sortingOrder, top.Canvas.sortingOrder);
            Assert.AreEqual(2, _manager.WindowCount);
        });

        [UnityTest]
        public IEnumerator Duplicate_ReplaceExisting_ClosesTheOpenWindowFirst() => UniTask.ToCoroutine(async () =>
        {
            var first = _manager.Open<TestWindow, int>();
            await WaitUntil(() => _manager.TopWindow, "shown");
            var firstWindow = (TestWindow)_manager.TopWindow;
            var hiddenBefore = firstWindow.Hidden;

            var replacement = await _manager.Show<TestWindow>(Duplicate(WindowDuplicatePolicy.ReplaceExisting));

            Assert.AreEqual(0, await first, "the replaced window returns the default");
            Assert.AreEqual(hiddenBefore + 1, firstWindow.Hidden);
            Assert.AreEqual(1, _manager.WindowCount);
            Assert.AreSame(replacement, _manager.TopWindow);
        });

        // ---- Queue ----

        [UnityTest]
        public IEnumerator Queue_OpensOneWindowAtATime_ByPriority_WhenNothingIsOpen() => UniTask.ToCoroutine(async () =>
        {
            var blocking = await _manager.Show<NoBackWindow>();
            var low = _manager.Enqueue<TestWindow, int>(new WindowOptions { Data = "low" }, priority: 0);
            var high = _manager.Enqueue<TestWindow, int>(new WindowOptions { Data = "high" }, priority: 5);
            await UniTask.NextFrame();

            Assert.AreEqual(2, _manager.QueuedCount, "waits while a window is open");
            Assert.AreEqual(1, _manager.WindowCount);

            await _manager.Close(blocking);
            await WaitUntil(() => _manager.TopWindow is TestWindow, "first queued window");
            Assert.AreEqual(1, _manager.QueuedCount);
            Assert.AreEqual(UniTaskStatus.Pending, low.Status);

            ((TestWindow)_manager.TopWindow).CloseWith(1);
            Assert.AreEqual(1, await high, "the higher priority goes first");

            await WaitUntil(() => _manager.TopWindow is TestWindow, "second queued window");
            ((TestWindow)_manager.TopWindow).CloseWith(2);
            Assert.AreEqual(2, await low);
            Assert.AreEqual(0, _manager.QueuedCount);
        });

        [UnityTest]
        public IEnumerator Queue_Ignore_DoesNotQueueTheSameWindowTwice_AndCancelRemoves() => UniTask.ToCoroutine(async () =>
        {
            await _manager.Show<NoBackWindow>();
            var first = _manager.Enqueue<TestWindow, int>(Duplicate(WindowDuplicatePolicy.Ignore));
            var second = _manager.Enqueue<TestWindow, int>(Duplicate(WindowDuplicatePolicy.Ignore));
            Assert.AreEqual(1, _manager.QueuedCount);

            using var cts = new CancellationTokenSource();
            var canceled = _manager.Enqueue<NoBackWindow, int>(null, cancellationToken: cts.Token);
            Assert.AreEqual(2, _manager.QueuedCount);
            cts.Cancel();
            Assert.AreEqual(1, _manager.QueuedCount);
            Assert.AreEqual(UniTaskStatus.Canceled, canceled.Status);

            _manager.ClearQueue();
            Assert.AreEqual(UniTaskStatus.Canceled, first.Status);
            Assert.AreEqual(UniTaskStatus.Canceled, second.Status);
        });

        // ---- Preload ----

        [UnityTest]
        public IEnumerator Preload_FillsThePool() => UniTask.ToCoroutine(async () =>
        {
            var prefab = _prefabs.Prefabs[typeof(TestWindow)];

            await _manager.PreloadAsync<TestWindow>(2);

            Assert.IsNotNull(_pool.GetPrefabStats(prefab), "the pool exists before the first opening");
            var window = await _manager.Show<TestWindow>();
            Assert.IsNotNull(window);
            Assert.GreaterOrEqual(_pool.GetPrefabStats(prefab).poolHits, 1, "opened from a preloaded instance");
        });

        // ---- Debug snapshot ----

        [UnityTest]
        public IEnumerator GetOpenWindows_ListsTheStackBottomToTop() => UniTask.ToCoroutine(async () =>
        {
            var bottom = await _manager.Show<TestWindow>();
            var top = await _manager.Show<NoBackWindow>();

            var windows = _manager.GetOpenWindows();

            Assert.AreEqual(2, windows.Count);
            Assert.AreSame(bottom, windows[0].Window);
            Assert.AreSame(top, windows[1].Window);
            Assert.AreEqual(WindowState.Open, windows[1].State);
            Assert.IsFalse(windows[1].CloseOnBackPress);
        });
    }
}
