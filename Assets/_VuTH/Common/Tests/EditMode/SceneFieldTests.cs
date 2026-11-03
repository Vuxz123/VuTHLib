using _VuTH.Common.Scene;
using NUnit.Framework;

namespace _VuTH.Common.Tests.EditMode
{
    public class SceneFieldTests
    {
        [Test]
        public void ConvertsImplicitlyToAndFromString()
        {
            SceneField field = "Home";
            string name = field;

            Assert.AreEqual("Home", name);
            Assert.AreEqual("Home", field.ToString());
        }

        [Test]
        public void IsEmpty_WhenNoSceneSelected()
        {
            Assert.IsTrue(default(SceneField).IsEmpty);
            Assert.IsTrue(((SceneField)"").IsEmpty);
            Assert.IsFalse(((SceneField)"Home").IsEmpty);
        }

        [Test]
        public void Equality_IsOrdinalOnTheSceneName()
        {
            Assert.AreEqual((SceneField)"Home", (SceneField)"Home");
            Assert.AreNotEqual((SceneField)"Home", (SceneField)"home");
            Assert.AreEqual(((SceneField)"Home").GetHashCode(), ((SceneField)"Home").GetHashCode());
            Assert.AreEqual(default(SceneField).GetHashCode(), default(SceneField).GetHashCode());
        }
    }
}
