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

        // ---- Row selection / reorder ----

        [Test]
        public void ApplySelectionClick_PlainCtrlAndShift()
        {
            var selected = new HashSet<int> { 1, 2 };

            DictionaryRowInteraction.ApplySelectionClick(selected, 4, ctrl: false, shift: false);
            CollectionAssert.AreEquivalent(new[] { 4 }, selected);

            DictionaryRowInteraction.ApplySelectionClick(selected, 6, ctrl: true, shift: false);
            CollectionAssert.AreEquivalent(new[] { 4, 6 }, selected);

            DictionaryRowInteraction.ApplySelectionClick(selected, 4, ctrl: true, shift: false);
            CollectionAssert.AreEquivalent(new[] { 6 }, selected);
        }

        [Test]
        public void ApplySelectionClick_ShiftSelectsRangeFromNearestSelected()
        {
            var selected = new HashSet<int> { 2, 9 };
            DictionaryRowInteraction.ApplySelectionClick(selected, 5, ctrl: false, shift: true);
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, selected);

            var empty = new HashSet<int>();
            DictionaryRowInteraction.ApplySelectionClick(empty, 3, ctrl: false, shift: true);
            CollectionAssert.AreEquivalent(new[] { 3 }, empty);
        }

        [Test]
        public void ComputePreciseInsertIndex_UsesRowCenters()
        {
            var rects = ThreeRows();
            var indices = new List<int> { 0, 1, 2 };

            Assert.AreEqual(0, DictionaryRowInteraction.ComputePreciseInsertIndex(rects, indices, 3, 5f));
            Assert.AreEqual(1, DictionaryRowInteraction.ComputePreciseInsertIndex(rects, indices, 3, 25f));
            Assert.AreEqual(3, DictionaryRowInteraction.ComputePreciseInsertIndex(rects, indices, 3, 100f));
            Assert.AreEqual(0, DictionaryRowInteraction.ComputePreciseInsertIndex(new List<Rect>(), new List<int>(), 0, 10f));
        }

        [Test]
        public void ComputeInsertLineY_HandlesEmptyFirstAndBetween()
        {
            var rects = ThreeRows();
            var indices = new List<int> { 0, 1, 2 };

            Assert.AreEqual(7f, DictionaryRowInteraction.ComputeInsertLineY(new List<Rect>(), new List<int>(), 0, 7f));
            Assert.AreEqual(-2f, DictionaryRowInteraction.ComputeInsertLineY(rects, indices, 0, 7f));
            Assert.AreEqual(20f, DictionaryRowInteraction.ComputeInsertLineY(rects, indices, 1, 7f));
            Assert.AreEqual(62f, DictionaryRowInteraction.ComputeInsertLineY(rects, indices, 3, 7f));
        }

        [TestCase(1, 4, 5, 3)]
        [TestCase(3, 0, 5, 0)]
        [TestCase(0, 10, 5, 4)]
        public void ComputeMoveTarget_AdjustsDownwardMovesAndClamps(int from, int insert, int count, int expected)
        {
            Assert.AreEqual(expected, DictionaryRowInteraction.ComputeMoveTarget(from, insert, count));
        }

        private static List<Rect> ThreeRows()
        {
            return new List<Rect>
            {
                new Rect(0f, 0f, 100f, 20f),
                new Rect(0f, 20f, 100f, 20f),
                new Rect(0f, 40f, 100f, 20f)
            };
        }
    }
}
