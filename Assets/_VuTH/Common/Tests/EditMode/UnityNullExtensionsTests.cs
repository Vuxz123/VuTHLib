using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Common.Tests.EditMode
{
    [TestFixture]
    public class UnityNullExtensionsTests
    {
        [Test]
        public void IsAlive_Null_ReturnsFalse()
        {
            object obj = null;
            Assert.IsFalse(obj.IsAlive());
        }

        [Test]
        public void IsAlive_PlainObject_ReturnsTrue()
        {
            Assert.IsTrue(new object().IsAlive());
        }

        [Test]
        public void IsAlive_DestroyedUnityObject_ReturnsFalse_ThoughReferenceIsNotNull()
        {
            var go = new GameObject("victim");
            Object.DestroyImmediate(go);

            Assert.IsFalse(ReferenceEquals(go, null), "precondition: C# reference is still non-null");
            Assert.IsFalse(go.IsAlive());
        }

        [Test]
        public void OrNull_ReferenceToDestroyedComponent_ReturnsRealNull()
        {
            var go = new GameObject("service");
            object service = go.AddComponent<BoxCollider>();
            Object.DestroyImmediate(go);

            // Plain ?. would still see a non-null reference here (e.g. an IPoolManager backed by a destroyed MonoBehaviour).
            Assert.IsNotNull(service);
            Assert.IsNull(service.OrNull());
        }

        [Test]
        public void OrNull_AliveComponent_ReturnsSameInstance()
        {
            var go = new GameObject("service");
            try
            {
                object service = go.AddComponent<BoxCollider>();
                Assert.AreSame(service, service.OrNull());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
