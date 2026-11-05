using _VuTH.Core.Window.UI;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Core.Window.Tests.EditMode
{
    /// <summary>Safe area anchors: the fitted edges follow the safe area, the others stay at the screen edge.</summary>
    [TestFixture]
    public class SafeAreaFitterTests
    {
        private static readonly Vector2Int Screen = new(1000, 2000);

        // A notch on top (100 px) and a home indicator at the bottom (50 px).
        private static readonly Rect SafeArea = new(0, 50, 1000, 1850);

        [Test]
        public void AllEdges_FollowTheSafeArea()
        {
            Assert.AreEqual(new Vector2(0f, 0.025f), SafeAreaFitter.ComputeAnchorMin(SafeArea, Screen, true, true));
            Assert.AreEqual(new Vector2(1f, 0.95f), SafeAreaFitter.ComputeAnchorMax(SafeArea, Screen, true, true));
        }

        [Test]
        public void UnfittedEdges_StayAtTheScreenEdge()
        {
            Assert.AreEqual(Vector2.zero, SafeAreaFitter.ComputeAnchorMin(SafeArea, Screen, false, false));
            Assert.AreEqual(Vector2.one, SafeAreaFitter.ComputeAnchorMax(SafeArea, Screen, false, false));
        }
    }
}
