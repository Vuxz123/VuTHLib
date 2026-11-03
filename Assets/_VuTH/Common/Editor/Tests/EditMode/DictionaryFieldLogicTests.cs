using System;
using System.Collections.Generic;
using _VuTH.Common.Editor.Field;
using NUnit.Framework;
using UnityEngine;

namespace _VuTH.Common.Editor.Tests.EditMode
{
    /// <summary>
    /// Tests for the non-UI logic extracted from DictionaryField.
    /// </summary>
    [TestFixture]
    public class DictionaryFieldLogicTests
    {
        // ---- JSON codec ----

        [Test]
        public void EscapeString_EscapesQuotesAndBackslashes()
        {
            Assert.AreEqual("a\\\"b\\\\c", DictionaryJsonCodec.EscapeString("a\"b\\c"));
            Assert.AreEqual(string.Empty, DictionaryJsonCodec.EscapeString(null));
        }

        [Test]
        public void FormatArray_ThenParse_RoundTrips()
        {
            var json = DictionaryJsonCodec.FormatArray(new List<(string, string)>
            {
                ("\"k1\"", "1"),
                ("\"" + DictionaryJsonCodec.EscapeString("k\"2") + "\"", "true")
            });

            Assert.AreEqual("[{\"key\":\"k1\",\"value\":1},{\"key\":\"k\\\"2\",\"value\":true}]", json);

            var parsed = DictionaryJsonCodec.ParseSimpleArray(json);
            Assert.AreEqual(2, parsed.Count);
            Assert.AreEqual(("k1", "1"), parsed[0]);
            Assert.AreEqual(("k\"2", "true"), parsed[1]);
        }

        [Test]
        public void ParseSimpleArray_IgnoresUnknownProperties_AndKeepsRawLiterals()
        {
            var parsed = DictionaryJsonCodec.ParseSimpleArray(" [ { \"key\" : \"a\\nb\" , \"extra\": 5, \"value\": null } ] ");

            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual("a\nb", parsed[0].key);
            Assert.AreEqual("null", parsed[0].val);
        }

        [Test]
        public void ParseSimpleArray_EmptyAndInvalidInput()
        {
            Assert.AreEqual(0, DictionaryJsonCodec.ParseSimpleArray("   ").Count);
            Assert.AreEqual(0, DictionaryJsonCodec.ParseSimpleArray("[]").Count);
            Assert.Throws<Exception>(() => DictionaryJsonCodec.ParseSimpleArray("{\"key\":1}"));
            Assert.Throws<Exception>(() => DictionaryJsonCodec.ParseSimpleArray("[1]"));
        }

        // ---- Key utilities ----

        [Test]
        public void MakeUniqueString_AppendsCounterUntilUnused()
        {
            Assert.AreEqual("Key", DictionaryKeyUtility.MakeUniqueString("Key", new HashSet<string>()));
            Assert.AreEqual("Key (2)", DictionaryKeyUtility.MakeUniqueString("Key", new HashSet<string> { "Key", "Key (1)" }));
            Assert.AreEqual("2", DictionaryKeyUtility.MakeUniqueString("", new HashSet<string> { "", "1" }));
        }

        [Test]
        public void MakeTemplateKey_UsesNewKeyWithNumericSuffix()
        {
            Assert.AreEqual("NewKey", DictionaryKeyUtility.MakeTemplateKey(new HashSet<string>()));
            Assert.AreEqual("NewKey2", DictionaryKeyUtility.MakeTemplateKey(new HashSet<string> { "NewKey", "NewKey1" }));
        }

        [Test]
        public void MatchesSearch_IsCaseInsensitiveOnKeyOrValue()
        {
            Assert.IsTrue(DictionaryKeyUtility.MatchesSearch("Hello", null, "ell"));
            Assert.IsTrue(DictionaryKeyUtility.MatchesSearch(null, "WORLD", "orl"));
            Assert.IsFalse(DictionaryKeyUtility.MatchesSearch("abc", "def", "xyz"));
        }

        // ---- Duplicate keys ----

        [Test]
        public void FindDuplicateWinners_UniqueKeys_AreAllMinusOne()
        {
            CollectionAssert.AreEqual(new[] { -1, -1, -1 }, DictionaryKeyUtility.FindDuplicateWinners(new[] { "a", "b", "c" }));
        }

        [Test]
        public void FindDuplicateWinners_PointsEveryDuplicateAtTheLastOccurrence()
        {
            // SerializableDictionary.OnAfterDeserialize uses this[key] = value, so the last entry wins.
            var winners = DictionaryKeyUtility.FindDuplicateWinners(new[] { "gem", "gold", "gem", "x", "gem" });
            CollectionAssert.AreEqual(new[] { 4, -1, 4, -1, 4 }, winners);
        }

        [Test]
        public void FindDuplicateWinners_IgnoresNullAndEmptyKeys()
        {
            var winners = DictionaryKeyUtility.FindDuplicateWinners(new[] { null, "", null, "", "k" });
            CollectionAssert.AreEqual(new[] { -1, -1, -1, -1, -1 }, winners);
        }

        [Test]
        public void FindDuplicateWinners_Empty_ReturnsEmpty()
        {
            CollectionAssert.IsEmpty(DictionaryKeyUtility.FindDuplicateWinners(System.Array.Empty<string>()));
        }
    }
}
