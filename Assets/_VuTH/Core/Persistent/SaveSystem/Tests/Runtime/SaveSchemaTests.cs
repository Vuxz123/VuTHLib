using System.Text.RegularExpressions;
using System.Threading.Tasks;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using _VuTH.Core.Persistent.SaveSystem.Migrate;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _VuTH.Core.Persistent.SaveSystem.Tests.Runtime
{
    /// <summary>
    /// Data with its own schema migrates on its own; large saves are encrypted off the main thread; delete removes the
    /// corrupted backup too.
    /// </summary>
    [TestFixture]
    public class SaveSchemaTests
    {
        public class Profile
        {
            public int Coins;
        }

        /// <summary>v1 stored "gold"; v2 calls it "coins".</summary>
        private sealed class RenameGoldToCoins : JsonSaveMigrator
        {
            public RenameGoldToCoins() : base(1, 2)
            {
            }

            protected override void Migrate(JObject data)
            {
                data["Coins"] = data["gold"];
                data.Remove("gold");
            }
        }

        private sealed class Gold
        {
            public int gold;
        }

        private InMemorySaveBackend _backend;
        private SaveService _service;

        [SetUp]
        public void SetUp()
        {
            _backend = new InMemorySaveBackend();
            _service = new SaveService(_backend, new NewtonsoftJsonSerializer(), new XorEncryptor());
        }

        [Test]
        public async Task OwnSchema_MigratesOnlyThatData()
        {
            await _service.SaveAsync("profile", new Gold { gold = 7 });
            await _service.SaveAsync("settings", new Profile { Coins = 3 });

            var profile = await _service.TryLoadAsync<Profile>("profile", new SaveSchema(2, new RenameGoldToCoins()));
            var settings = await _service.TryLoadAsync<Profile>("settings");

            Assert.AreEqual(SaveLoadStatus.Loaded, profile.Status);
            Assert.AreEqual(7, profile.Data.Coins);
            Assert.AreEqual(SaveLoadStatus.Loaded, settings.Status, "other data needs no migrator");
            Assert.AreEqual(3, settings.Data.Coins);
        }

        [Test]
        public async Task OwnSchema_WritesItsVersion()
        {
            var schema = new SaveSchema(5);
            await _service.SaveAsync("profile", new Profile { Coins = 1 }, schema);
            LogAssert.Expect(LogType.Error, new Regex("UnsupportedVersion"));

            var withServiceVersion = await _service.TryLoadAsync<Profile>("profile");
            var withSchema = await _service.TryLoadAsync<Profile>("profile", schema);

            Assert.AreEqual(SaveLoadStatus.UnsupportedVersion, withServiceVersion.Status);
            Assert.AreEqual(SaveLoadStatus.Loaded, withSchema.Status);
        }

        [Test]
        public void Schema_RejectsInvalidMigrators()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new SaveSchema(3, new DefaultSaveMigrator(1, 2), new DefaultSaveMigrator(1, 3)));
        }

        [Test]
        public async Task LargeSave_EncryptedOnAWorkerThread_RoundTrips()
        {
            var service = new SaveService(_backend, new NewtonsoftJsonSerializer(), new DefaultAesEncryptor())
            {
                ThreadPoolEncryptionThreshold = 0,
            };

            await service.SaveAsync("big", new Profile { Coins = 99 });
            var result = await service.TryLoadAsync<Profile>("big");

            Assert.AreEqual(99, result.Data.Coins);
        }

        private sealed class MainThreadOnlyEncryptor : IEncryptor
        {
            public string Encrypt(string plainText) => plainText;
            public string Decrypt(string cipherText) => cipherText;
        }

        [Test]
        public void ThreadSafety_IsDeclaredByTheEncryptor()
        {
            Assert.IsTrue(EncryptorThreading.IsThreadSafe(new DefaultAesEncryptor()));
            Assert.IsTrue(EncryptorThreading.IsThreadSafe(new CompositeEncryptor(new XorEncryptor(), new Base64Encryptor())));
            Assert.IsFalse(EncryptorThreading.IsThreadSafe(new MainThreadOnlyEncryptor()));
            Assert.IsFalse(EncryptorThreading.IsThreadSafe(new CompositeEncryptor(new XorEncryptor(), new MainThreadOnlyEncryptor())));
        }

        [Test]
        public async Task Delete_RemovesTheCorruptedBackupToo()
        {
            await _backend.SaveRawAsync("k", "garbage");
            LogAssert.Expect(LogType.Error, new Regex("Corrupted"));
            await _service.TryLoadAsync<Profile>("k");
            Assert.IsNotNull(_backend.Raw("k" + SaveService.CorruptedBackupSuffix));

            await _service.DeleteAsync("k");

            Assert.IsNull(_backend.Raw("k"));
            Assert.IsNull(_backend.Raw("k" + SaveService.CorruptedBackupSuffix));
        }
    }
}
