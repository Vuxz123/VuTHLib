using _VuTH.Core.GameCycle.Screen.Core;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ScreenNavigationState"/>: base/stack/override/previous rules.
    /// </summary>
    public sealed class ScreenNavigationStateTests
    {
        private ScreenNavigationState _state;
        private ScreenModel _a;
        private ScreenModel _b;
        private ScreenModel _c;

        [SetUp]
        public void SetUp()
        {
            _state = new ScreenNavigationState();
            _a = CreateScreen("A");
            _b = CreateScreen("B");
            _c = CreateScreen("C");
        }

        [TearDown]
        public void TearDown()
        {
            if (_a) Object.DestroyImmediate(_a);
            if (_b) Object.DestroyImmediate(_b);
            if (_c) Object.DestroyImmediate(_c);
        }

        private static ScreenModel CreateScreen(string name)
        {
            var screen = ScriptableObject.CreateInstance<ScreenModel>();
            screen.name = name;
            return screen;
        }

        [Test]
        public void EffectiveCurrent_EmptyState_IsNull()
        {
            Assert.IsNull(_state.EffectiveCurrent);
            Assert.IsNull(_state.BottomOfStack);
            Assert.AreEqual(0, _state.StackCount);
            Assert.IsFalse(_state.CanPop);
            Assert.IsFalse(_state.IsOverriding);
        }

        [Test]
        public void EffectiveCurrent_EmptyStack_FallsBackToBase()
        {
            _state.Base = _a;

            Assert.AreSame(_a, _state.EffectiveCurrent);
        }

        [Test]
        public void EffectiveCurrent_StackTopWinsOverBase()
        {
            _state.Base = _a;
            _state.Push(_b);
            _state.Push(_c);

            Assert.AreSame(_c, _state.EffectiveCurrent);
            Assert.AreSame(_c, _state.Peek());
            Assert.AreEqual(2, _state.StackCount);
        }

        [Test]
        public void Override_WinsOverStack_AndClearingRestoresStackTop()
        {
            _state.Push(_a);
            _state.Push(_b);
            _state.Override = _c;

            Assert.IsTrue(_state.IsOverriding);
            Assert.AreSame(_c, _state.EffectiveCurrent);

            _state.Override = null;

            Assert.IsFalse(_state.IsOverriding);
            Assert.AreSame(_b, _state.EffectiveCurrent);
        }

        [Test]
        public void Override_Destroyed_IsTreatedAsNotOverriding()
        {
            _state.Push(_a);
            _state.Override = _c;

            Object.DestroyImmediate(_c);

            Assert.IsFalse(_state.IsOverriding);
            Assert.AreSame(_a, _state.EffectiveCurrent);
        }

        [Test]
        public void CanPop_RequiresAtLeastTwoStackEntries()
        {
            _state.Push(_a);
            Assert.IsFalse(_state.CanPop);

            _state.Push(_b);
            Assert.IsTrue(_state.CanPop);
        }

        [Test]
        public void Pop_ReturnsTop_AndExposesNextAsPeek()
        {
            _state.Push(_a);
            _state.Push(_b);

            var popped = _state.Pop();

            Assert.AreSame(_b, popped);
            Assert.AreSame(_a, _state.Peek());
            Assert.AreSame(_a, _state.EffectiveCurrent);
            Assert.AreEqual(1, _state.StackCount);
        }

        [Test]
        public void BottomOfStack_IsFirstPushed()
        {
            _state.Push(_a);
            _state.Push(_b);
            _state.Push(_c);

            Assert.AreSame(_a, _state.BottomOfStack);
        }

        [Test]
        public void PopIfTop_RemovesOnlyMatchingTop()
        {
            _state.Push(_a);
            _state.Push(_b);

            Assert.IsFalse(_state.PopIfTop(_a));
            Assert.AreEqual(2, _state.StackCount);

            Assert.IsTrue(_state.PopIfTop(_b));
            Assert.AreEqual(1, _state.StackCount);
            Assert.AreSame(_a, _state.Peek());
        }

        [Test]
        public void PopIfTop_EmptyStack_ReturnsFalse()
        {
            Assert.IsFalse(_state.PopIfTop(_a));
        }

        [Test]
        public void TakeAll_ReturnsTopToBottom_AndClearsStack()
        {
            _state.Base = _a;
            _state.Push(_a);
            _state.Push(_b);
            _state.Push(_c);

            var taken = _state.TakeAll();

            CollectionAssert.AreEqual(new[] { _c, _b, _a }, taken);
            Assert.AreEqual(0, _state.StackCount);
            Assert.AreSame(_a, _state.EffectiveCurrent, "Base screen is kept and becomes effective again.");
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            _state.Base = _a;
            _state.Push(_b);
            _state.Override = _c;
            _state.Previous = _a;

            _state.Reset();

            Assert.IsNull(_state.Base);
            Assert.IsNull(_state.Override);
            Assert.IsNull(_state.Previous);
            Assert.AreEqual(0, _state.StackCount);
            Assert.IsNull(_state.EffectiveCurrent);
        }
    }
}
