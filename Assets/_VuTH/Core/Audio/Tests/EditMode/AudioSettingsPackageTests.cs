using NUnit.Framework;

namespace _VuTH.Core.Audio.Tests.EditMode
{
    /// <summary>
    /// The audio settings payload mapping is generated, and volumes stay within 0..1 whatever their source: a constructor
    /// argument, the game, or a damaged / edited save.
    /// </summary>
    public class AudioSettingsPackageTests
    {
        [Test]
        public void ExtractPayload_MapsEveryField_ToItsCamelCaseMember()
        {
            var package = new AudioSettingsPackage(muted: true, masterVolume: 0.1f, musicVolume: 0.2f,
                sfxVolume: 0.3f, uiVolume: 0.4f);

            var payload = package.ExtractPayload();

            Assert.IsTrue(payload.muted);
            Assert.AreEqual(0.1f, payload.masterVolume);
            Assert.AreEqual(0.2f, payload.musicVolume);
            Assert.AreEqual(0.3f, payload.sfxVolume);
            Assert.AreEqual(0.4f, payload.uiVolume);
            package.Dispose();
        }

        [Test]
        public void InjectPayload_ClampsOutOfRangeVolumes_AndDoesNotMarkDirty()
        {
            var package = new AudioSettingsPackage();

            package.InjectPayload(new AudioSettingsPayload
            {
                muted = true, masterVolume = 3f, musicVolume = -1f, sfxVolume = 0.5f, uiVolume = 1.5f
            });

            Assert.IsTrue(package.Muted.Value);
            Assert.AreEqual(1f, package.MasterVolume.Value);
            Assert.AreEqual(0f, package.MusicVolume.Value);
            Assert.AreEqual(0.5f, package.SfxVolume.Value);
            Assert.AreEqual(1f, package.UiVolume.Value);
            Assert.IsFalse(package.IsDirty);
            package.Dispose();
        }

        [Test]
        public void SettingAVolume_IsClamped_AndAnUnchangedClampedValueIsNotAChange()
        {
            var package = new AudioSettingsPackage(masterVolume: 2f);
            Assert.AreEqual(1f, package.MasterVolume.Value, "constructor value clamped");

            package.MasterVolume.Value = 5f; // clamps to 1, the current value

            Assert.IsFalse(package.IsDirty, "no change after clamping");
            package.MasterVolume.Value = -2f;
            Assert.AreEqual(0f, package.MasterVolume.Value);
            Assert.IsTrue(package.IsDirty);
            package.Dispose();
        }
    }
}
