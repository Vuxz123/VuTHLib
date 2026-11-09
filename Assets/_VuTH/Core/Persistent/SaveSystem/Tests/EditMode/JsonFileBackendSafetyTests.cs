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

        [Test]
        public async Task PreviousHashedName_IsReadableButNeverOverwrittenOrResurrected()
        {
            Directory.CreateDirectory(_tempDir);
            var oldPath = Path.Combine(_tempDir, "a_b-3a8e75c1.json");
            File.WriteAllText(oldPath, "old-format");
            Assert.AreEqual("old-format", await _backend.LoadRawAsync("a/b"));
            await _backend.SaveRawAsync("a/b", "new-format");
            Assert.AreEqual("old-format", File.ReadAllText(oldPath));
            await _backend.DeleteAsync("a/b");
            Assert.IsNull(await new JsonFileSaveBackend(_tempDir).LoadRawAsync("a/b"));
        }

        [Test]
        public async Task LongUnicodeKeys_RoundTripWithoutFilenameLengthErrors()
        {
            var key = new string('\u1ec7', 100);
            await _backend.SaveRawAsync(key, "unicode");
            Assert.AreEqual("unicode", await _backend.LoadRawAsync(key));
        }

        [Test]
        public async Task EncodedAndLiteralKeys_DoNotShareStorage()
        {
            _backend.SaveRaw("a/b", "slash-data");
            _backend.SaveRaw("a_b-3a8e75c1", "literal-data");
            Assert.AreEqual("slash-data", await _backend.LoadRawAsync("a/b"));
            Assert.AreEqual("literal-data", await _backend.LoadRawAsync("a_b-3a8e75c1"));
        }

        [Test]
        public async Task CaseSensitiveKeys_HaveIndependentFilesAndTickets()
        {
            var ticket = _backend.ReserveWrite();
            _backend.SaveRaw("Slot", "newer");
            await _backend.SaveRawOrderedAsync("slot", "older", ticket);
            Assert.AreEqual("newer", await _backend.LoadRawAsync("Slot"));
            Assert.AreEqual("older", await _backend.LoadRawAsync("slot"));
        }

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
        public async Task AsyncSaveStartedBeforeASyncFlush_DoesNotReplaceIt()
        {
            // Focus lost starts an async save; the pause that follows flushes newer data synchronously.
            var olderAsync = _backend.SaveRawAsync("slot", "older");
            _backend.SaveRaw("slot", "newer");
            await olderAsync;

            Assert.AreEqual("newer", await _backend.LoadRawAsync("slot"));
            CollectionAssert.IsEmpty(Directory.GetFiles(_tempDir, "*.tmp"));
        }

        [Test]
        public async Task LoadRawAsync_MissingKey_ReturnsNull()
        {
            Assert.IsNull(await _backend.LoadRawAsync("missing"));
            Assert.IsFalse(await _backend.ExistsAsync("missing"));
        }

        [Test]
        public async Task Delete_InvalidatesAnOlderReservedWrite()
        {
            var ticket = _backend.ReserveWrite();
            await _backend.DeleteAsync("slot");
            await _backend.SaveRawOrderedAsync("slot", "old", ticket);
            Assert.IsNull(await _backend.LoadRawAsync("slot"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Delete_DoesNotResurrectLegacySave_AfterRestart(bool writeNewSave)
        {
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(Path.Combine(_tempDir, "slot_1.json"), "legacy");
            if (writeNewSave) await _backend.SaveRawAsync("slot/1", "new");

            await _backend.DeleteAsync("slot/1");

            var restarted = new JsonFileSaveBackend(_tempDir);
            Assert.IsFalse(await restarted.ExistsAsync("slot/1"));
            Assert.IsNull(await restarted.LoadRawAsync("slot/1"));
            Assert.AreEqual("legacy", await restarted.LoadRawAsync("slot_1"));
            await restarted.SaveRawAsync("slot/1", "restored");
            Assert.AreEqual("restored", await restarted.LoadRawAsync("slot/1"));
        }
    }
}
