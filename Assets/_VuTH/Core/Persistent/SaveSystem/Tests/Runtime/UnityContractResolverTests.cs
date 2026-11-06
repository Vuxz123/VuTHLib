using System;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Serialization;

namespace _VuTH.Core.Persistent.SaveSystem.Tests.Runtime
{
    /// <summary>The save serializer follows Unity's serialization attributes, as the Inspector does.</summary>
    [TestFixture]
    public class UnityContractResolverTests
    {
        [Serializable]
        public class RenamedDto
        {
            [FormerlySerializedAs("gold")] public long coins;
            [SerializeField] private int secret = 3;
            [NonSerialized] public int cache = 9;

            public int Secret => secret;
        }

        [Serializable]
        public class OldDto
        {
            public long gold;
        }

        private readonly NewtonsoftJsonSerializer _serializer = new();

        [Test]
        public void FormerlySerializedAs_ReadsSavesWrittenBeforeTheRename()
        {
            var oldSave = _serializer.Serialize(new OldDto { gold = 42 });

            var loaded = _serializer.Deserialize<RenamedDto>(oldSave);

            Assert.AreEqual(42, loaded.coins);
        }

        [Test]
        public void FormerlySerializedAs_WritesOnlyTheNewName()
        {
            var json = _serializer.Serialize(new RenamedDto { coins = 5 });

            StringAssert.Contains("\"coins\":5", json);
            StringAssert.DoesNotContain("gold", json);
        }

        [Test]
        public void SerializeFieldIsSaved_AndNonSerializedIsNot()
        {
            var json = _serializer.Serialize(new RenamedDto());

            StringAssert.Contains("secret", json);
            StringAssert.DoesNotContain("cache", json);
            Assert.AreEqual(3, _serializer.Deserialize<RenamedDto>(json).Secret);
        }
    }
}
