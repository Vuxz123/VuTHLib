using System.Collections.Generic;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Core;
using _VuTH.Common.MessagePipe.Editor;
using NUnit.Framework;

namespace _VuTH.Common.Editor.Tests.EditMode
{
    /// <summary>
    /// Tests for the non-UI logic extracted from MessagePipeSettingTab.
    /// </summary>
    [TestFixture]
    public class MessagePipeSettingsLogicTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void EventDisplayName_NullOrEmpty_ReturnsUnknown(string input)
        {
            Assert.AreEqual(EventDisplayNameFormatter.UnknownName, EventDisplayNameFormatter.Format(input));
            Assert.AreEqual("(unknown)", EventDisplayNameFormatter.Format(input));
        }

        [TestCase("My.Game.Events.PlayerDied, Game, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "My.Game.Events.PlayerDied")]
        [TestCase("Global.Foo, Asm", "Foo")]
        [TestCase("NoNamespace, Asm, Version=1.0.0.0", "NoNamespace")]
        [TestCase("Plain", "Plain")]
        public void EventDisplayName_FormatsAssemblyQualifiedNames(string input, string expected)
        {
            Assert.AreEqual(expected, EventDisplayNameFormatter.Format(input));
        }

        [Test]
        public void EventScopeGrouping_SeparatesGlobalAndScene_AndSortsSceneNames()
        {
            var g1 = Entry(EventScope.Global, null);
            var z1 = Entry(EventScope.Scene, "Zeta");
            var a1 = Entry(EventScope.Scene, "Alpha");
            var g2 = Entry(EventScope.Global, "Ignored");
            var z2 = Entry(EventScope.Scene, "Zeta");

            var grouping = EventScopeGrouping.Group(new List<EventScopeEntry> { g1, z1, a1, g2, z2 });

            Assert.AreEqual(5, grouping.TotalCount);
            CollectionAssert.AreEqual(new[] { g1, g2 }, grouping.GlobalEvents);
            CollectionAssert.AreEqual(new[] { "Alpha", "Zeta" }, grouping.SortedSceneNames);
            CollectionAssert.AreEqual(new[] { z1, z2 }, grouping.SceneEventsByScene["Zeta"]);
            CollectionAssert.AreEqual(new[] { a1 }, grouping.SceneEventsByScene["Alpha"]);
        }

        [Test]
        public void EventScopeGrouping_EmptySceneName_GroupsUnderPlaceholder()
        {
            var e1 = Entry(EventScope.Scene, "");
            var e2 = Entry(EventScope.Scene, null);

            var grouping = EventScopeGrouping.Group(new List<EventScopeEntry> { e1, e2 });

            Assert.AreEqual(0, grouping.GlobalEvents.Count);
            Assert.AreEqual(1, grouping.SceneEventsByScene.Count);
            CollectionAssert.AreEqual(new[] { e1, e2 }, grouping.SceneEventsByScene[EventScopeGrouping.EmptySceneName]);
        }

        [Test]
        public void AssemblyWhitelist_NormalizeName_Trims()
        {
            Assert.AreEqual("MyGameplay", AssemblyWhitelistEditing.NormalizeName("  MyGameplay \t"));
            Assert.AreEqual(string.Empty, AssemblyWhitelistEditing.NormalizeName("   "));
            Assert.IsNull(AssemblyWhitelistEditing.NormalizeName(null));
        }

        [Test]
        public void AssemblyWhitelist_ContainsName_IsCaseInsensitive()
        {
            var names = new List<string> { "VuTH.Gameplay", "VuTH.Core" };

            Assert.IsTrue(AssemblyWhitelistEditing.ContainsName(names, "vuth.gameplay"));
            Assert.IsTrue(AssemblyWhitelistEditing.ContainsName(names, "VUTH.CORE"));
            Assert.IsFalse(AssemblyWhitelistEditing.ContainsName(names, "VuTH.Common"));
        }

        [Test]
        public void AssemblyWhitelist_ReflectionField_Exists()
        {
            Assert.IsNotNull(AssemblyWhitelistEditing.GetAssemblyNamesField());
        }

        private static EventScopeEntry Entry(EventScope scope, string sceneName)
        {
            return new EventScopeEntry { typeFullName = "Ns.T, Asm", scope = scope, sceneName = sceneName };
        }
    }
}
