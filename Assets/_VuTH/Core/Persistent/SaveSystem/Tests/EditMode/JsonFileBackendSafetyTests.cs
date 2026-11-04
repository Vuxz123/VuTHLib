using System;
using System.IO;
using System.Threading.Tasks;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using NUnit.Framework;

namespace _VuTH.Core.Persistent.SaveSystem.Tests.EditMode
{
    /// <summary>Async writes are atomic, and keys never collide on disk.</summary>
    [TestFixture]
    public class JsonFileBackendSafetyTests
    {
        private string _tempDir;
        private JsonFileSaveBackend _backend;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"VuTH_JsonSafety_{Guid.NewGuid():N}");
            _backend = new JsonFileSaveBackend(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        [Test]
        public async Task SaveRawAsync_Overwrites_ThroughATempFile_AndLeavesNoneBehind()
        {
            await _backend.SaveRawAsync("slot", "old");
            await _backend.SaveRawAsync("slot", "new");

            Assert.AreEqual("new", await _backend.LoadRawAsync("slot"));
            CollectionAssert.IsEmpty(Directory.GetFiles(_tempDir, "*.tmp"));
        }

        [Test]
        public async Task KeysThatSanitizeAlike_DoNotShareAFile()
        {
            await _backend.SaveRawAsync("a/b", "slash");
            await _backend.SaveRawAsync("a_b", "underscore");

            Assert.AreEqual("slash", await _backend.LoadRawAsync("a/b"));
            Assert.AreEqual("underscore", await _backend.LoadRawAsync("a_b"));
        }

        [Test]
        public async Task KeyWithInvalidCharacters_StillReadsAFileWrittenByTheOldNaming()
        {
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(Path.Combine(_tempDir, "slot_1.json"), "legacy");

            Assert.IsTrue(await _backend.ExistsAsync("slot/1"));
            Assert.AreEqual("legacy", await _backend.LoadRawAsync("slot/1"));

            await _backend.SaveRawAsync("slot/1", "new");
            Assert.AreEqual("new", await _backend.LoadRawAsync("slot/1"), "its own file wins once written");
        }

        [Test]
        public async Task LoadRawAsync_MissingKey_ReturnsNull()
        {
            Assert.IsNull(await _backend.LoadRawAsync("missing"));
            Assert.IsFalse(await _backend.ExistsAsync("missing"));
        }
    }
}
