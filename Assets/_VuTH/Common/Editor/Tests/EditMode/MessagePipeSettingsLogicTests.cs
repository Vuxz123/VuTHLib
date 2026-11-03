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
    }
}
