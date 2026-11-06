using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using _VuTH.Core.Pool;
using _VuTH.Core.Window.Blocker;
using _VuTH.Core.Window.ScreenIntegration;
using _VuTH.Core.Window.Transition;
using _VuTH.Core.Window.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace _VuTH.Core.Window.Tests.PlayMode
{
    /// <summary>
    /// Kept instances, Screen changes, toasts, window events, outside clicks, typed windows, tabs, sequence and
    /// element transitions, preset assets, and the shipped WindowManager prefab.
    /// </summary>
    public class WindowFeaturePlayModeTests
    {
        public class PlainPopup : PopupBase
        {
            public void CloseWith(object result) => Close(result);
        }

        public class KeptPopup : PopupBase
        {
            public int State;
            public override bool KeepInstance => true;
        }

        public class PersistentPopup : PopupBase
        {
            public override bool KeepOnScreenChange => true;
        }

        public class TestToast : ToastBase
        {
        }

        public class OutsidePopup : PopupBase
        {
            public override bool CloseOnOutsideClick => true;
        }

        public class TypedPopup : PopupBase<string, int>
        {
            public string Received;
            protected override void OnSetup(string data) => Received = data;
            public void Answer(int value) => Close(value);
        }

        public class TabbedPopup : PopupBase
        {
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

        private readonly List<GameObject> _owned = new();
        private WindowManager _manager;
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
            Template<PlainPopup>();
            Template<KeptPopup>();
            Template<PersistentPopup>();
            Template<TestToast>();
            Template<TypedPopup>();

            var outside = Template<OutsidePopup>();
            var area = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            area.transform.SetParent(outside.transform, false);
            SetField(outside.GetComponent<OutsidePopup>(), "outsideClickArea", area.GetComponent<Image>());

            var tabbed = Template<TabbedPopup>();
            var tabA = new GameObject("TabA", typeof(RectTransform));
            var tabB = new GameObject("TabB", typeof(RectTransform));
            tabA.transform.SetParent(tabbed.transform, false);
            tabB.transform.SetParent(tabbed.transform, false);
            var group = tabbed.AddComponent<WindowTabGroup>();
            SetField(group, "tabs", new[] { tabA, tabB });

            var managerGo = new GameObject("WindowManager_Test");
            _owned.Add(managerGo);
            _manager = managerGo.AddComponent<WindowManager>()
                .WithPrefabProvider(_prefabs)
                .WithInputBlocker(null)
                .WithTransitionFactory(null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager) Object.DestroyImmediate(_manager.gameObject);
            foreach (var go in _owned)
            {
                if (go) Object.DestroyImmediate(go);
            }

            _owned.Clear();
            foreach (var view in Object.FindObjectsByType<UIViewBase>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        private GameObject Template<T>() where T : UIViewBase
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.SetActive(false);
            go.AddComponent<T>();
            _owned.Add(go);
            _prefabs.Prefabs[typeof(T)] = go;
            return go;
        }

        private static void SetField(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }

            Assert.Fail($"Field {name} not found on {target.GetType().Name}");
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

        // ---- Kept instance ----

        [UnityTest]
        public IEnumerator KeepInstance_ReusesTheSameObject_WithItsState() => UniTask.ToCoroutine(async () =>
        {
            var first = await _manager.Show<KeptPopup>();
            first.State = 5;
            await _manager.Close(first);
            Assert.IsFalse(first.gameObject.activeSelf, "kept hidden");

            var second = await _manager.Show<KeptPopup>();

            Assert.AreSame(first, second);
            Assert.AreEqual(5, second.State);
            await _manager.Close(second);
        });

        // ---- Screen change ----

        [UnityTest]
        public IEnumerator ScreenChange_ClosesWindows_ExceptThoseThatKeepOpen() => UniTask.ToCoroutine(async () =>
        {
            await _manager.Show<PlainPopup>();
            var persistent = await _manager.Show<PersistentPopup>();

            new CloseWindowsOnScreenChange().OnPreScreenExit(default);
            await WaitUntil(() => _manager.WindowCount == 1, "closed");

            Assert.AreSame(persistent, _manager.TopWindow);
        });

        // ---- Toast ----

        [UnityTest]
        public IEnumerator Toast_ClosesByItself_AndDoesNotBlockClicks() => UniTask.ToCoroutine(async () =>
        {
            var toast = _manager.ShowToast<TestToast>(duration: 0.1f);
            await WaitUntil(() => _manager.HasWindow<TestToast>(), "toast shown");
            var view = _manager.GetWindow<TestToast>();

            Assert.IsFalse(view.CanvasGroup.blocksRaycasts);
            await toast.Timeout(TimeSpan.FromSeconds(3), DelayType.Realtime);
            Assert.AreEqual(0, _manager.WindowCount);
        });

        [UnityTest]
        public IEnumerator Toast_DoesNotTakeTheBackPress_NorHoldTheQueue() => UniTask.ToCoroutine(async () =>
        {
            var popup = await _manager.Show<PlainPopup>();
            _manager.ShowToast<TestToast>(duration: 10f).Forget();
            await WaitUntil(() => _manager.HasWindow<TestToast>(), "toast on top");

            Assert.IsTrue(await _manager.HandleBackPressAsync(), "the popup below the toast gets it");
            await WaitUntil(() => !popup.IsOpen, "popup closed");
            Assert.IsTrue(_manager.HasWindow<TestToast>(), "the toast stays");

            var queued = _manager.Enqueue<PlainPopup>();
            await WaitUntil(() => _manager.HasWindow<PlainPopup>(), "queued popup opens despite the toast");
            await _manager.CloseAll();
            await queued;
        });

        // ---- Events ----

        [UnityTest]
        public IEnumerator WindowEvents_ReportOpenAndCloseWithTheCause() => UniTask.ToCoroutine(async () =>
        {
            var events = new List<WindowEvent>();
            _manager.OnWindowEvent += events.Add;

            var first = _manager.Open<PlainPopup, int>();
            await WaitUntil(() => _manager.TopWindow, "shown");
            ((PlainPopup)_manager.TopWindow).CloseWith(3);
            await first;

            await _manager.Show<PlainPopup>();
            await _manager.HandleBackPressAsync();
            await WaitUntil(() => _manager.WindowCount == 0, "closed by back");

            Assert.AreEqual(4, events.Count);
            Assert.AreEqual(WindowEventType.Opened, events[0].Type);
            Assert.AreEqual(nameof(PlainPopup), events[0].WindowName);
            Assert.AreEqual(WindowCloseCause.Closed, events[1].CloseCause);
            Assert.IsTrue(events[1].HasResult);
            Assert.AreEqual(WindowCloseCause.BackPress, events[3].CloseCause);
            Assert.IsFalse(events[3].HasResult);
        });

        // ---- Outside click ----

        [UnityTest]
        public IEnumerator OutsideClick_ClosesThePopup() => UniTask.ToCoroutine(async () =>
        {
            var popup = await _manager.Show<OutsidePopup>();
            var area = popup.transform.Find("Dimmer").GetComponent<IPointerClickHandler>();
            Assert.IsNotNull(area, "click catcher added on the outside area");

            area.OnPointerClick(null);
            await WaitUntil(() => _manager.WindowCount == 0, "closed");
        });

        // ---- Typed ----

        [UnityTest]
        public IEnumerator TypedWindow_ReceivesTypedData_AndReturnsTypedResult() => UniTask.ToCoroutine(async () =>
        {
            var opening = _manager.Open<TypedPopup, string, int>("hello");
            await WaitUntil(() => _manager.TopWindow, "shown");
            var popup = (TypedPopup)_manager.TopWindow;

            Assert.AreEqual("hello", popup.Received);
            popup.Answer(9);
            Assert.AreEqual(9, await opening);

            LogAssert.Expect(LogType.Error, new Regex("expects String data"));
            var wrong = await _manager.Show<TypedPopup>(42);
            Assert.IsNull(wrong.Received);
        });

        // ---- Tabs ----

        [UnityTest]
        public IEnumerator Tabs_BackReturnsToThePreviousTab_ThenCloses() => UniTask.ToCoroutine(async () =>
        {
            var popup = await _manager.Show<TabbedPopup>();
            var tabs = popup.GetComponent<WindowTabGroup>();
            Assert.AreEqual(0, tabs.CurrentTab);
            tabs.SelectTab(1);

            Assert.IsTrue(await _manager.HandleBackPressAsync());
            Assert.AreEqual(0, tabs.CurrentTab);
            Assert.AreEqual(1, _manager.WindowCount, "still open");

            Assert.IsTrue(await _manager.HandleBackPressAsync());
            await WaitUntil(() => _manager.WindowCount == 0, "closed");

            // The next opening starts on the default tab again.
            tabs.SelectTab(1);
            var again = await _manager.Show<TabbedPopup>();
            Assert.AreEqual(0, again.GetComponent<WindowTabGroup>().CurrentTab);
        });

        // ---- Transitions ----

        [UnityTest]
        public IEnumerator SequenceTransition_AndElements_EndInTheRestState() => UniTask.ToCoroutine(async () =>
        {
            var template = _prefabs.Prefabs[typeof(PlainPopup)];
            var button = new GameObject("Button", typeof(RectTransform));
            button.transform.SetParent(template.transform, false);
            SetField(button.AddComponent<UITransitionElement>(), "duration", 0.05f);

            var factoryGo = new GameObject("TransitionFactory_Test");
            _owned.Add(factoryGo);
            _manager.WithTransitionFactory(factoryGo.AddComponent<UITransitionFactory>());

            var sequence = new SequenceTransitionSettings
            {
                parallel = true,
                steps = { new FadeTransitionSettings { duration = 0.05f }, new SlideTransitionSettings { duration = 0.05f } },
            };

            var popup = await _manager.Show<PlainPopup>(new WindowOptions
            {
                TransitionInSettings = sequence,
                TransitionOutSettings = sequence,
            }).Timeout(TimeSpan.FromSeconds(3), DelayType.Realtime);

            Assert.AreEqual(1f, popup.CanvasGroup.alpha);
            var element = popup.transform.Find("Button");
            Assert.AreEqual(Vector3.one, element.localScale, "element played to its rest scale");
            Assert.AreEqual(1f, element.GetComponent<CanvasGroup>().alpha);
        });

        [Test]
        public void PresetAsset_OverridesTheBuiltInPresetOfTheSameName()
        {
            var factoryGo = new GameObject("TransitionFactory_Test");
            _owned.Add(factoryGo);
            var factory = factoryGo.AddComponent<UITransitionFactory>();
            var preset = ScriptableObject.CreateInstance<UITransitionPresetAsset>();
            preset.name = "Scale";
            SetField(preset, "transition", new FadeTransitionSettings { duration = 0.1f });

            factory.RegisterPreset(preset);

            Assert.IsInstanceOf<FadeTransition>(factory.Create("scale"));
            Assert.IsInstanceOf<SlideTransition>(factory.Create("Slide:Left"));
            Object.DestroyImmediate(preset);
        }

        // ---- Review fixes ----

        public class DimmedPopup : PopupBase
        {
        }

        public class FirmPopup : PopupBase
        {
            public override bool AllowBackClose => false;
        }

        public class FullPopup : FullScreenBase
        {
        }

        /// <summary>Holds the transitions of non-toast windows until released.</summary>
        private sealed class GateFactory : IUITransitionFactory
        {
            public UniTaskCompletionSource Gate = new();
            public IUITransition Create(UITransitionSettings settings) => new Gated(this);
            public IUITransition Create(string presetName) => new Gated(this);

            private sealed class Gated : IUITransition
            {
                private readonly GateFactory _factory;
                public Gated(GateFactory factory) => _factory = factory;
                public float Duration => 0f;
                public UniTask In(IUIView view) => Run(view, true);
                public UniTask Out(IUIView view) => Run(view, false);

                private async UniTask Run(IUIView view, bool visible)
                {
                    if (view is not ToastBase) await _factory.Gate.Task;
                    view.CanvasGroup.alpha = visible ? 1f : 0f;
                    view.CanvasGroup.interactable = visible;
                    view.CanvasGroup.blocksRaycasts = visible;
                }
            }
        }

        private GameObject DimmedTemplate()
        {
            var template = Template<DimmedPopup>();
            var dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            dimmer.transform.SetParent(template.transform, false);
            SetField(template.GetComponent<DimmedPopup>(), "dimmerCanvasGroup", dimmer.GetComponent<CanvasGroup>());
            return template;
        }

        [UnityTest]
        public IEnumerator ReleasePrefab_SkippedWhileOpen_AndClearsThePoolOtherwise() => UniTask.ToCoroutine(async () =>
        {
            var prefab = _prefabs.Prefabs[typeof(PlainPopup)];
            var popup = await _manager.Show<PlainPopup>();

            _manager.ReleasePrefab<PlainPopup>();
            CollectionAssert.IsEmpty(_prefabs.Released, "an open window still uses the prefab");

            await _manager.Close(popup);
            Assert.IsNotNull(_pool.GetPrefabStats(prefab), "the closed instance waits in the pool");

            _manager.ReleasePrefab<PlainPopup>();
            CollectionAssert.AreEqual(new[] { typeof(PlainPopup) }, _prefabs.Released);
            Assert.IsNull(_pool.GetPrefabStats(prefab), "pooled instances released with the prefab");
        });

        [UnityTest]
        public IEnumerator ClearPrefabCache_ReleasesOnlyTypesNotOpen() => UniTask.ToCoroutine(async () =>
        {
            await _manager.Show<PlainPopup>();
            var kept = await _manager.Show<KeptPopup>();
            await _manager.Close(kept);

            _manager.ClearPrefabCache();

            CollectionAssert.AreEqual(new[] { typeof(KeptPopup) }, _prefabs.Released);
            await UniTask.NextFrame();
            Assert.IsTrue(kept == null, "the kept instance is released with its prefab");
        });

        [UnityTest]
        public IEnumerator TopWindow_AndCloseTop_SkipToasts() => UniTask.ToCoroutine(async () =>
        {
            var popup = await _manager.Show<PlainPopup>();
            _manager.ShowToast<TestToast>(duration: 10f).Forget();
            await WaitUntil(() => _manager.HasWindow<TestToast>(), "toast shown");

            Assert.AreSame(popup, _manager.TopWindow);
            await _manager.CloseTop();

            Assert.IsFalse(_manager.HasWindow<PlainPopup>(), "the popup closed, not the toast");
            Assert.IsTrue(_manager.HasWindow<TestToast>());
        });

        [UnityTest]
        public IEnumerator Toast_DoesNotWaitForAPopupTransition() => UniTask.ToCoroutine(async () =>
        {
            var gate = new GateFactory();
            _manager.WithTransitionFactory(gate);

            var popup = _manager.Show<PlainPopup>();
            _manager.ShowToast<TestToast>(duration: 10f).Forget();

            await WaitUntil(() => _manager.GetWindow<TestToast>() != null &&
                                  _manager.GetWindow<TestToast>().CanvasGroup.alpha > 0f, "toast shown");
            Assert.AreEqual(UniTaskStatus.Pending, popup.Status, "the popup is still transitioning in");

            gate.Gate.TrySetResult();
            await popup;
        });

        [UnityTest]
        public IEnumerator ForceClose_StopsTheDimmerTween() => UniTask.ToCoroutine(async () =>
        {
            DimmedTemplate();
            var factoryGo = new GameObject("TransitionFactory_Test");
            _owned.Add(factoryGo);
            _manager.WithTransitionFactory(factoryGo.AddComponent<UITransitionFactory>());

            _manager.Show<DimmedPopup>(new WindowOptions { TransitionInSettings = new FadeTransitionSettings { duration = 2f } })
                .Forget();
            await WaitUntil(() => _manager.GetWindow<DimmedPopup>() != null, "opening");
            var popup = _manager.GetWindow<DimmedPopup>();
            await UniTask.Yield();
            Assert.Greater(Tween.GetTweensCount(popup), 0, "the dimmer tween targets the popup");

            await _manager.CloseAll(forceCleanup: true);

            Assert.AreEqual(0, Tween.GetTweensCount(popup));
        });

        [UnityTest]
        public IEnumerator ForceClose_StopsElementTweens() => UniTask.ToCoroutine(async () =>
        {
            var template = _prefabs.Prefabs[typeof(PlainPopup)];
            var child = new GameObject("Element", typeof(RectTransform));
            child.transform.SetParent(template.transform, false);
            SetField(child.AddComponent<UITransitionElement>(), "duration", 2f);

            _manager.Show<PlainPopup>().Forget();
            await WaitUntil(() => _manager.GetWindow<PlainPopup>() != null, "opening");
            await UniTask.Yield();
            var group = _manager.GetWindow<PlainPopup>().transform.Find("Element").GetComponent<CanvasGroup>();
            Assert.Greater(Tween.GetTweensCount(group), 0);

            await _manager.CloseAll(forceCleanup: true);

            Assert.AreEqual(0, Tween.GetTweensCount(group));
        });

        [UnityTest]
        public IEnumerator BackInput_ReachingNoWindow_IsReported() => UniTask.ToCoroutine(async () =>
        {
            var unhandled = 0;
            _manager.OnBackPressUnhandled += () => unhandled++;

            await _manager.ProcessBackInputAsync();
            Assert.AreEqual(1, unhandled);

            await _manager.Show<PlainPopup>();
            await _manager.ProcessBackInputAsync();
            Assert.AreEqual(1, unhandled, "a window took it");
        });

        [UnityTest]
        public IEnumerator BackPress_CallerOptionOverridesTheWindow() => UniTask.ToCoroutine(async () =>
        {
            Template<FirmPopup>();
            await _manager.Show<FirmPopup>(new WindowOptions { CloseOnBackPress = true });

            Assert.IsTrue(await _manager.HandleBackPressAsync());
            await WaitUntil(() => _manager.WindowCount == 0, "closed: there is a single switch");
        });

        [UnityTest]
        public IEnumerator GetWindow_ReturnsTheTopmost() => UniTask.ToCoroutine(async () =>
        {
            await _manager.Show<PlainPopup>();
            var top = await _manager.Show<PlainPopup>();

            Assert.AreSame(top, _manager.GetWindow<PlainPopup>());
            Assert.AreSame(top, _manager.GetWindow<PopupBase>(), "derived types match");
        });

        [UnityTest]
        public IEnumerator FullScreen_HidesTheWindowsBelow_AndReportsTheStack() => UniTask.ToCoroutine(async () =>
        {
            Template<FullPopup>();
            var states = new List<WindowStackState>();
            _manager.OnStackChanged += states.Add;

            var below = await _manager.Show<PlainPopup>();
            var full = await _manager.Show<FullPopup>();

            Assert.IsFalse(below.Canvas.enabled, "covered: not rendered");
            Assert.IsTrue(_manager.StackState.IsScreenCovered);
            Assert.AreEqual(2, _manager.StackState.ModalCount);

            await _manager.Close(full);
            Assert.IsTrue(below.Canvas.enabled);
            Assert.IsFalse(_manager.StackState.IsScreenCovered);

            await _manager.Close(below);
            Assert.IsFalse(_manager.StackState.HasModal);
            Assert.IsTrue(states.Exists(s => s.IsScreenCovered));
            Assert.IsFalse(states[^1].HasModal);
        });

        [UnityTest]
        public IEnumerator OutsideClick_CanBeTurnedOnPerOpening() => UniTask.ToCoroutine(async () =>
        {
            DimmedTemplate();
            var popup = await _manager.Show<DimmedPopup>(new WindowOptions { CloseOnOutsideClick = true });

            popup.transform.Find("Dimmer").GetComponent<IPointerClickHandler>().OnPointerClick(null);

            await WaitUntil(() => _manager.WindowCount == 0, "closed by the outside click");
        });

        [UnityTest]
        public IEnumerator Open_BeforeInitialization_LoadsTheProfile() => UniTask.ToCoroutine(async () =>
        {
            var go = new GameObject("WindowManager_NotInitialized");
            go.SetActive(false);
            _owned.Add(go);
            var manager = go.AddComponent<WindowManager>().WithPrefabProvider(_prefabs);

            var popup = await manager.Show<PlainPopup>();

            Assert.IsNotNull(popup, "no NullReferenceException on the profile");
            await manager.Close(popup);
        });
    }

    /// <summary>The shipped WindowManager prefab is wired correctly (blocker, transition factory).</summary>
    public class WindowManagerPrefabTests
    {
        public class PrefabTestPopup : PopupBase
        {
        }

        private sealed class PrefabProvider : IWindowPrefabProvider
        {
            public GameObject Prefab;
            public UniTask<GameObject> GetOrLoad(Type windowType) => UniTask.FromResult(Prefab);
            public bool TryGetLoaded(Type windowType, out GameObject prefab)
            {
                prefab = Prefab;
                return prefab;
            }

            public void Release(Type windowType) { }
            public void Clear() { }
        }

        private GameObject _instance;
        private GameObject _template;

        [TearDown]
        public void TearDown()
        {
            if (_instance) Object.DestroyImmediate(_instance);
            if (_template) Object.DestroyImmediate(_template);
            foreach (var view in Object.FindObjectsByType<PrefabTestPopup>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Prefab_BlocksInputOnlyDuringTransitions_WithRealTransitions() => UniTask.ToCoroutine(async () =>
        {
            var prefab = Resources.Load<GameObject>("WindowManager");
            Assert.IsNotNull(prefab, "Resources/WindowManager.prefab");
            _instance = Object.Instantiate(prefab);
            var manager = _instance.GetComponent<WindowManager>();

            Assert.IsInstanceOf<UIInputBlocker>(manager.InputBlocker, "input blocker wired");
            Assert.IsFalse(manager.InputBlocker.IsBlocked);

            _template = new GameObject("PrefabTestPopup", typeof(RectTransform));
            _template.SetActive(false);
            _template.AddComponent<PrefabTestPopup>();
            manager.WithPrefabProvider(new PrefabProvider { Prefab = _template });

            var showing = manager.Show<PrefabTestPopup>();
            await UniTask.Yield();
            Assert.IsTrue(manager.InputBlocker.IsBlocked, "blocked during the Scale transition (factory wired)");

            var popup = await showing.Timeout(TimeSpan.FromSeconds(3), DelayType.Realtime);
            Assert.IsFalse(manager.InputBlocker.IsBlocked, "free once shown");
            Assert.IsTrue(popup.CanvasGroup.interactable);

            await manager.Close(popup).Timeout(TimeSpan.FromSeconds(3), DelayType.Realtime);
            Assert.IsFalse(manager.InputBlocker.IsBlocked);
        });
    }
}
