using System;
using System.Text.RegularExpressions;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Transition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _VuTH.Core.GameCycle.ScreenFlow.Tests.EditMode
{
    [TestFixture]
    public class ScreenFlowActorTests
    {
        private GraphBuilder _b;
        private FakeScreenManager _nav;
        private ScreenModel _home, _shop, _game;

        [SetUp]
        public void SetUp()
        {
            _b = new GraphBuilder();
            _nav = new FakeScreenManager();
            _home = _b.Screen("Home");
            _shop = _b.Screen("Shop");
            _game = _b.Screen("Game");

            // home --shop--> shop --back--> home ; home --play--> game
            _b.Node("home", _home, start: true);
            _b.Node("shop", _shop);
            _b.Node("game", _game);
            _b.Edge("home", "shop", "shop");
            _b.Edge("shop", "home", "back");
            _b.Edge("home", "game", "play");
        }

        [TearDown]
        public void TearDown() => _b.Dispose();

        private ScreenFlowActor CreateActor(out ScreenFlowStateContainer state, Func<IScreenManager> provider = null)
        {
            state = new ScreenFlowStateContainer();
            return new ScreenFlowActor(state, new ScreenFlowGraphResolver(_b.Graph), new TransitionContext("test"),
                provider ?? (() => _nav));
        }

        [Test]
        public void StartFlow_EntersStartScreenOnce()
        {
            var actor = CreateActor(out _);

            actor.StartFlow();
            actor.StartFlow();

            CollectionAssert.AreEqual(new[] { _home }, _nav.Entered);
            Assert.IsTrue(actor.IsStarted);
        }

        [Test]
        public void StartFlow_WithoutNavigator_CanBeRetriedLater()
        {
            IScreenManager available = null;
            var actor = CreateActor(out _, () => available);

            actor.StartFlow();
            Assert.IsFalse(actor.IsStarted);

            available = _nav;
            actor.StartFlow();
            CollectionAssert.AreEqual(new[] { _home }, _nav.Entered);
        }

        [Test]
        public void Trigger_ResolvesAndEntersNextScreen_AndAdvancesState()
        {
            var actor = CreateActor(out var state);

            actor.Trigger("shop");

            CollectionAssert.AreEqual(new[] { _shop }, _nav.Entered);
            Assert.AreSame(_shop, state.Current);
            Assert.AreEqual("shop", state.LastEvent);
        }

        [Test]
        public void TriggersDuringTransition_AreQueued_AndRunAfterItCompletes()
        {
            var actor = CreateActor(out var state);
            _nav.HoldTransitions = true;

            actor.Trigger("shop");
            actor.Trigger("back");
            Assert.AreEqual(1, actor.PendingEventCount);
            CollectionAssert.AreEqual(new[] { _shop }, _nav.Entered);

            _nav.CompleteTransition();

            CollectionAssert.AreEqual(new[] { _shop, _home }, _nav.Entered);
            Assert.AreSame(_home, state.Current);
        }

        [Test]
        public void NonMatchingQueuedEvent_DoesNotBlockTheNextOne()
        {
            var actor = CreateActor(out var state);
            _nav.HoldTransitions = true;

            actor.Trigger("shop");
            actor.Trigger("play");   // no 'play' edge from shop -> dropped
            actor.Trigger("back");   // valid from shop
            _nav.CompleteTransition();

            CollectionAssert.AreEqual(new[] { _shop, _home }, _nav.Entered);
            Assert.AreSame(_home, state.Current);
            Assert.AreEqual(0, actor.PendingEventCount);
        }

        [Test]
        public void FailedTransition_IsLogged_AndQueueKeepsDraining()
        {
            var actor = CreateActor(out _);
            _nav.HoldTransitions = true;

            actor.Trigger("shop");
            actor.Trigger("back");

            LogAssert.Expect(LogType.Error, new Regex("navigation to 'Shop' failed"));
            _nav.FailTransition(new InvalidOperationException("scene missing"));

            CollectionAssert.AreEqual(new[] { _shop, _home }, _nav.Entered);
        }

        [Test]
        public void ResolvedNodeWithoutScreen_DoesNotChangeState()
        {
            _b.Node("empty", null);
            _b.Edge("home", "empty", "void");
            var actor = CreateActor(out var state);

            actor.Trigger("void");

            Assert.IsEmpty(_nav.Entered);
            Assert.AreSame(_home, state.Current);
        }

        [Test]
        public void Dispose_BeforeAnyNavigation_DoesNotThrow_AndUnsubscribes()
        {
            var neverStarted = CreateActor(out _, () => null);
            Assert.DoesNotThrow(neverStarted.Dispose);

            var actor = CreateActor(out _);
            actor.Trigger("shop");
            Assert.IsTrue(_nav.HasSubscribers);

            actor.Dispose();
            Assert.IsFalse(_nav.HasSubscribers);
            actor.Trigger("back");
            Assert.AreEqual(1, _nav.Entered.Count, "a disposed actor ignores triggers");
        }

        [Test]
        public void TryGetPrevious_OneStepBack_IsThePreviousNode_NotTheCurrent()
        {
            var actor = CreateActor(out var state);
            actor.Trigger("shop");
            actor.Trigger("back");

            // history: home, shop, home(current)
            Assert.IsTrue(state.TryGetPrevious(1, out var previous));
            Assert.AreSame(_shop, previous.Screen);

            Assert.IsTrue(state.TryGetPrevious(2, out var twoBack));
            Assert.AreSame(_home, twoBack.Screen);

            Assert.IsFalse(state.TryGetPrevious(3, out _));
            Assert.IsFalse(state.TryGetPrevious(0, out _));
        }
    }
}
