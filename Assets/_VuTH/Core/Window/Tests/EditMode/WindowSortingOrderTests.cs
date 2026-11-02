using NUnit.Framework;

namespace _VuTH.Core.Window.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for <see cref="WindowSortingOrder"/>.
    /// </summary>
    [TestFixture]
    public class WindowSortingOrderTests
    {
        private const int PopupBase = 1000;
        private const int SystemBase = 2000;
        private const int Step = 10;

        private static int Compute(WindowType type, int countAfterPush)
        {
            return WindowSortingOrder.Compute(type, countAfterPush, PopupBase, SystemBase, Step);
        }

        [TestCase(WindowType.Popup, 1000)]
        [TestCase(WindowType.FullScreenPopup, 1000)]
        [TestCase(WindowType.System, 2000)]
        [TestCase(WindowType.Tutorial, 3000)]
        public void Compute_FirstWindow_UsesBaseOrderForType(WindowType type, int expected)
        {
            Assert.AreEqual(expected, Compute(type, 1));
        }

        [Test]
        public void Compute_AddsStepPerStackIndex()
        {
            Assert.AreEqual(PopupBase + 2 * Step, Compute(WindowType.Popup, 3));
            Assert.AreEqual(SystemBase + 4 * Step, Compute(WindowType.System, 5));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void Compute_NonPositiveCount_ClampsIndexToZero(int count)
        {
            Assert.AreEqual(PopupBase, Compute(WindowType.Popup, count));
        }

        [Test]
        public void Compute_UnknownWindowType_FallsBackToPopupBase()
        {
            Assert.AreEqual(PopupBase + Step, Compute((WindowType)999, 2));
        }
    }
}
