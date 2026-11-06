using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace _VuTH.Core.Persistent.DataPackage.Tests.EditMode
{
    /// <summary>
    /// The source generator runs inside Unity: packages marked [GeneratePayloadMapping] get ExtractPayload / InjectPayload
    /// mapped by field name (also ignoring case), with [PayloadMember] and [PayloadIgnore] honored.
    /// </summary>
    [TestFixture]
    public partial class PayloadMappingGeneratorTests
    {
        public class SettingsDto
        {
            public float masterVolume = 1f; // camelCase, as in AudioSettingsPayload
            public bool muted;
            public int coins;
        }

        [GeneratePayloadMapping]
        public partial class SettingsPackage : PersistencePackage<SettingsDto>
        {
            public PersistentField<float> MasterVolume { get; }
            public PersistentField<bool> Muted { get; }
            [PayloadMember("coins")] public PersistentField<int> Gold { get; }
            [PayloadIgnore] public PersistentField<int> SessionClicks { get; }

            public SettingsPackage() : base("generated_settings", SaveStrategy.ManualOnly)
            {
                MasterVolume = new PersistentField<float>(this, 1f);
                Muted = new PersistentField<bool>(this);
                Gold = new PersistentField<int>(this);
                SessionClicks = new PersistentField<int>(this);
            }
        }

        [Test]
        public void ExtractPayload_IsGenerated_FromTheFields()
        {
            var package = new SettingsPackage();
            package.MasterVolume.Value = 0.4f;
            package.Muted.Value = true;
            package.Gold.Value = 12;
            package.SessionClicks.Value = 99;

            var dto = package.ExtractPayload();

            Assert.AreEqual(0.4f, dto.masterVolume);
            Assert.IsTrue(dto.muted);
            Assert.AreEqual(12, dto.coins);
            package.Dispose();
        }

        [Test]
        public void InjectPayload_IsGenerated_AndDoesNotMarkDirty()
        {
            var package = new SettingsPackage();

            package.InjectPayload(new SettingsDto { masterVolume = 0.2f, muted = true, coins = 7 });

            Assert.AreEqual(0.2f, package.MasterVolume.Value);
            Assert.IsTrue(package.Muted.Value);
            Assert.AreEqual(7, package.Gold.Value);
            Assert.AreEqual(0, package.SessionClicks.Value, "ignored");
            Assert.IsFalse(package.IsDirty);
            package.Dispose();
        }

        [Test]
        public void PlayerProfile_UsesTheGeneratedMapping()
        {
            var profile = new PlayerProfilePackage();
            profile.InjectPayload(new PlayerProfileDTO { PlayerName = "Vu", Level = 3, Gold = 40, Exp = 5, HighScore = 9 });

            var dto = profile.ExtractPayload();

            Assert.AreEqual("Vu", dto.PlayerName);
            Assert.AreEqual(3, dto.Level);
            Assert.AreEqual(40, dto.Gold);
            Assert.AreEqual(5, dto.Exp);
            Assert.AreEqual(9, dto.HighScore);
            profile.Dispose();
        }

        // ---- User field attributes ----

        public sealed class StepAttribute : Attribute, IPersistentFieldNormalizer<float>
        {
            private readonly float _step;
            public StepAttribute(float step) => _step = step;
            public float Normalize(float value) => Mathf.Round(value / _step) * _step;
        }

        public sealed class CurrencyField : PersistentField<long>
        {
            public CurrencyField(IPersistencePackage owner, long value) : base(owner, value) { }
            public bool CanAfford(long price) => Value >= price;
        }

        public sealed class CurrencyAttribute : Attribute, IPersistentFieldFactory<long, CurrencyField>
        {
            public CurrencyField Create(IPersistencePackage owner, long defaultValue) => new CurrencyField(owner, defaultValue);
        }

        public class WalletDto
        {
            [UnityEngine.Range(0f, 0.9f), Step(0.25f)] public float volume = 0.5f;
            [Currency] public long gems = 10;
        }

        [GeneratePersistentFields]
        public partial class WalletPackage : PersistencePackage<WalletDto>
        {
            public WalletPackage() : base("generated_wallet", SaveStrategy.ManualOnly)
            {
            }
        }

        [Test]
        public void UserNormalizer_RunsInUnity_WithinTheRange()
        {
            var package = new WalletPackage();

            package.Volume.Value = 0.6f;
            Assert.AreEqual(0.5f, package.Volume.Value, "rounded to the 0.25 step");
            package.Volume.Value = 7f;
            Assert.AreEqual(0.9f, package.Volume.Value, 1e-6f, "the step gives 1, clamped back to the 0.9 maximum");
            package.Dispose();
        }

        [Test]
        public void UserFactory_ChoosesTheFieldType()
        {
            var package = new WalletPackage();

            CurrencyField gems = package.Gems; // the generated property has the factory's type
            Assert.IsTrue(gems.CanAfford(10));
            Assert.AreEqual(10, package.ExtractPayload().gems);
            package.Dispose();
        }

        [Test]
        public void PlayerProfile_RefusesToSpendMoreGoldThanOwned()
        {
            var profile = new PlayerProfilePackage();
            profile.Gold.Value = 50;
            LogAssert.Expect(LogType.Error, new Regex("Rejected -50"));

            var spent = profile.Gold.TrySetValue(profile.Gold.Value - 100);

            Assert.IsFalse(spent, "a clamp would have left 0 and let the purchase through");
            Assert.AreEqual(50, profile.Gold.Value);
            profile.Dispose();
        }

        [Test]
        public void GeneratedField_FirstUsedAfterDispose_IsDisposedAtOnce()
        {
            var package = new WalletPackage();
            package.Dispose();

            // Created now, then disposed at once instead of staying registered in the disposed package.
            Assert.Throws<ObjectDisposedException>(() => package.Volume.Observable.Subscribe(_ => { }));
        }
    }
}
