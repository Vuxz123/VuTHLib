using System.Text.RegularExpressions;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Transition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _VuTH.Core.GameCycle.ScreenFlow.Tests.EditMode
{
    /// <summary>Transition navigation modes (Enter / Push / PushOverride / Pop / PopOverride) and event-name checks.</summary>
    [TestFixture]
    public class ScreenFlowNavigationTests
    {
        private GraphBuilder _b;
        private FakeScreenManager _nav;
        private ScreenModel _home, _shop, _pause;

        [SetUp]
        public void SetUp()
        {
            _b = new GraphBuilder();
            _nav = new FakeScreenManager();
            _home = _b.Screen("Home");
            _shop = _b.Screen("Shop");
            _pause = _b.Screen("Pause");
            _b.Node("home", _home, start: true);
            _b.Node("shop", _shop);
            _b.Node("pause", _pause);
        }

        [TearDown]
        public void TearDown() => _b.Dispose();

        private ScreenFlowActor Actor(out ScreenFlowStateContainer state)
        {
            state = new ScreenFlowStateContainer();
            return new ScreenFlowActor(state, new ScreenFlowGraphResolver(_b.Graph), new TransitionContext("test"), () => _nav);
        }

        [Test]
        public void Push_UsesTargetScreenSetting_ByDefault()
        {
            _shop.hideScreenBelow = true;
            _b.Edge("home", "shop", "shop", navigation: ScreenFlowNavigation.Push);

            Actor(out _).Trigger("shop");

            CollectionAssert.AreEqual(new[] { "Push:Shop:hide=True" }, _nav.Ops);
        }

        [TestCase(ScreenBelowVisibility.Hide, "Push:Shop:hide=True")]
        [TestCase(ScreenBelowVisibility.KeepVisible, "Push:Shop:hide=False")]
        public void Push_TransitionSetting_OverridesScreenSetting(ScreenBelowVisibility below, string expected)
        {
            _shop.hideScreenBelow = below != ScreenBelowVisibility.Hide; // opposite of what the transition asks
            _b.Edge("home", "shop", "shop", navigation: ScreenFlowNavigation.Push, screenBelow: below);

            Actor(out _).Trigger("shop");

            CollectionAssert.AreEqual(new[] { expected }, _nav.Ops);
        }

        [Test]
        public void PushOverride_AndPopOverride_AreForwarded()
        {
            _b.Edge("home", "pause", "pause", navigation: ScreenFlowNavigation.PushOverride);
            _b.Edge("pause", "home", "resume", navigation: ScreenFlowNavigation.PopOverride);
            var actor = Actor(out var state);

            actor.Trigger("pause");
            _nav.IsOverriding = true;
            actor.Trigger("resume");

            CollectionAssert.AreEqual(new[] { "PushOverride:Pause", "PopOverride" }, _nav.Ops);
            Assert.AreSame(_home, state.Current);
        }

        [Test]
        public void Pop_WhenSomethingToPop_CallsPop()
        {
            _b.Edge("home", "shop", "shop", navigation: ScreenFlowNavigation.Push);
            _b.Edge("shop", "home", "back", navigation: ScreenFlowNavigation.Pop);
            var actor = Actor(out var state);

            actor.Trigger("shop");
            _nav.CanPop = true;
            actor.Trigger("back");

            CollectionAssert.AreEqual(new[] { "Push:Shop:hide=False", "Pop" }, _nav.Ops);
            Assert.AreSame(_home, state.Current);
        }

        [Test]
        public void Pop_WithNothingToPop_FallsBackToEnter()
        {
            _b.Edge("home", "shop", "shop");
            _b.Edge("shop", "home", "back", navigation: ScreenFlowNavigation.Pop);
            var actor = Actor(out _);
            actor.Trigger("shop");

            LogAssert.Expect(LogType.Warning, new Regex("nothing to pop"));
            actor.Trigger("back");

            CollectionAssert.AreEqual(new[] { "Enter:Shop", "Enter:Home" }, _nav.Ops);
        }

        [Test]
        public void PopOverride_WithoutOverride_FallsBackToEnter()
        {
            _b.Edge("home", "pause", "resume", navigation: ScreenFlowNavigation.PopOverride);

            LogAssert.Expect(LogType.Warning, new Regex("no override is active"));
            Actor(out _).Trigger("resume");

            CollectionAssert.AreEqual(new[] { "Enter:Pause" }, _nav.Ops);
        }

        [Test]
        public void UnknownEventName_IsReported_AndNotQueued()
        {
            _b.Edge("home", "shop", "shop");
            var actor = Actor(out var state);

            LogAssert.Expect(LogType.Warning, new Regex("'shpo' does not exist"));
            actor.Trigger("shpo");

            Assert.IsEmpty(_nav.Ops);
            Assert.AreEqual(0, actor.PendingEventCount);
            Assert.AreSame(_home, state.Current);
        }

        [Test]
        public void Resolver_HasEvent_ReflectsGraphEventNames()
        {
            _b.Edge("home", "shop", "shop");
            var resolver = new ScreenFlowGraphResolver(_b.Graph);

            Assert.IsTrue(resolver.HasEvent("shop"));
            Assert.IsFalse(resolver.HasEvent("Shop"), "event names are case-sensitive");
            Assert.IsFalse(resolver.HasEvent(null));
        }
    }
}
