using NUnit.Framework;

namespace _VuTH.Core.Window.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for <see cref="WindowStack{TView}"/> using plain C# view stand-ins.
    /// </summary>
    [TestFixture]
    public class WindowStackTests
    {
        private class FakeView
        {
        }

        private sealed class FakePopup : FakeView
        {
        }

        private sealed class FakeScreen : FakeView
        {
        }

        private WindowStack<FakeView> _stack;

        [SetUp]
        public void SetUp()
        {
            _stack = new WindowStack<FakeView>();
        }

        [Test]
        public void Empty_HasNoTopAndZeroCount()
        {
            Assert.AreEqual(0, _stack.Count);
            Assert.IsNull(_stack.Top);
        }

        [Test]
        public void Push_LastPushedIsTop()
        {
            var a = new FakeView();
            var b = new FakeView();

            _stack.Push(a);
            _stack.Push(b);

            Assert.AreEqual(2, _stack.Count);
            Assert.AreSame(b, _stack.Top);
            CollectionAssert.AreEqual(new[] { a, b }, _stack.ToArray());
        }

        [Test]
        public void Remove_MiddleWindow_KeepsOrderAndTop()
        {
            var a = new FakeView();
            var b = new FakeView();
            var c = new FakeView();
            _stack.Push(a);
            _stack.Push(b);
            _stack.Push(c);

            Assert.IsTrue(_stack.Remove(b));

            CollectionAssert.AreEqual(new[] { a, c }, _stack.ToArray());
            Assert.AreSame(c, _stack.Top);
            Assert.IsFalse(_stack.Contains(b));
        }

        [Test]
        public void Remove_Top_ExposesPreviousWindow()
        {
            var a = new FakeView();
            var b = new FakeView();
            _stack.Push(a);
            _stack.Push(b);

            _stack.Remove(b);

            Assert.AreSame(a, _stack.Top);
        }

        [Test]
        public void Remove_UnknownWindow_ReturnsFalse()
        {
            _stack.Push(new FakeView());

            Assert.IsFalse(_stack.Remove(new FakeView()));
            Assert.AreEqual(1, _stack.Count);
        }

        [Test]
        public void ToArray_IsSnapshot()
        {
            var a = new FakeView();
            _stack.Push(a);

            var snapshot = _stack.ToArray();
            _stack.ClearWindows();

            Assert.AreEqual(1, snapshot.Length);
            Assert.AreSame(a, snapshot[0]);
            Assert.AreEqual(0, _stack.Count);
        }

        [Test]
        public void TypeQueries_FindFirstMatchFromBottom()
        {
            var screen = new FakeScreen();
            var popup1 = new FakePopup();
            var popup2 = new FakePopup();
            _stack.Push(screen);
            _stack.Push(popup1);
            _stack.Push(popup2);

            Assert.IsTrue(_stack.HasWindowOfType<FakePopup>());
            Assert.AreSame(popup1, _stack.GetWindowOfType<FakePopup>());
            Assert.AreSame(screen, _stack.GetWindowOfType<FakeView>());
        }

        [Test]
        public void TypeQueries_NoMatch_ReturnFalseAndNull()
        {
            _stack.Push(new FakeScreen());

            Assert.IsFalse(_stack.HasWindowOfType<FakePopup>());
            Assert.IsNull(_stack.GetWindowOfType<FakePopup>());
        }

        [Test]
        public void Options_SetGetRemove()
        {
            var a = new FakeView();
            var options = new WindowOptions();

            _stack.SetOptions(a, options);

            Assert.IsTrue(_stack.TryGetOptions(a, out var stored));
            Assert.AreSame(options, stored);

            Assert.IsTrue(_stack.RemoveOptions(a));
            Assert.IsFalse(_stack.TryGetOptions(a, out _));
            Assert.IsFalse(_stack.RemoveOptions(a));
        }

        [Test]
        public void Options_AreIndependentOfStackMembership()
        {
            var a = new FakeView();
            var options = new WindowOptions();
            _stack.SetOptions(a, options);
            _stack.Push(a);

            _stack.ClearWindows();

            Assert.AreEqual(0, _stack.Count);
            Assert.IsTrue(_stack.TryGetOptions(a, out _), "ClearWindows must not drop options.");
        }

        [Test]
        public void Clear_RemovesWindowsAndOptions()
        {
            var a = new FakeView();
            _stack.SetOptions(a, new WindowOptions());
            _stack.Push(a);

            _stack.Clear();

            Assert.AreEqual(0, _stack.Count);
            Assert.IsFalse(_stack.TryGetOptions(a, out _));
        }
    }
}
