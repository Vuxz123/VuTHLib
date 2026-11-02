using NUnit.Framework;

namespace _VuTH.Core.Audio.Tests.EditMode
{
    public class AudioVolumeMixerTests
    {
        private const float Tolerance = 0.0001f;

        [TestCase(AudioChannel.Music, 0.1f)]
        [TestCase(AudioChannel.Sfx, 0.2f)]
        [TestCase(AudioChannel.Ui, 0.3f)]
        public void SelectChannelVolume_ReturnsVolumeOfChannel(AudioChannel channel, float expected)
        {
            var volume = AudioVolumeMixer.SelectChannelVolume(channel, musicVolume: 0.1f, sfxVolume: 0.2f, uiVolume: 0.3f);

            Assert.That(volume, Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void SelectChannelVolume_UnknownChannel_FallsBackToSfx()
        {
            var volume = AudioVolumeMixer.SelectChannelVolume((AudioChannel)99, musicVolume: 0.1f, sfxVolume: 0.2f, uiVolume: 0.3f);

            Assert.That(volume, Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void Resolve_WhenMuted_ReturnsZero()
        {
            var volume = AudioVolumeMixer.Resolve(AudioChannel.Music, 1f, muted: true, 1f, 1f, 1f, 1f);

            Assert.That(volume, Is.EqualTo(0f));
        }

        [Test]
        public void Resolve_MultipliesRequestedChannelAndMaster()
        {
            var volume = AudioVolumeMixer.Resolve(AudioChannel.Sfx, 0.8f, muted: false, masterVolume: 0.5f, musicVolume: 1f, sfxVolume: 0.25f, uiVolume: 1f);

            Assert.That(volume, Is.EqualTo(0.1f).Within(Tolerance));
        }

        [Test]
        public void Resolve_UsesVolumeOfPlaybackChannel()
        {
            var music = AudioVolumeMixer.Resolve(AudioChannel.Music, 1f, false, 1f, musicVolume: 0.4f, sfxVolume: 0.6f, uiVolume: 0.7f);
            var ui = AudioVolumeMixer.Resolve(AudioChannel.Ui, 1f, false, 1f, musicVolume: 0.4f, sfxVolume: 0.6f, uiVolume: 0.7f);

            Assert.That(music, Is.EqualTo(0.4f).Within(Tolerance));
            Assert.That(ui, Is.EqualTo(0.7f).Within(Tolerance));
        }

        [Test]
        public void Resolve_ClampsEveryFactorToUnitRange()
        {
            var overOne = AudioVolumeMixer.Resolve(AudioChannel.Sfx, 2f, false, masterVolume: 3f, musicVolume: 1f, sfxVolume: 5f, uiVolume: 1f);
            var negativeMaster = AudioVolumeMixer.Resolve(AudioChannel.Sfx, 1f, false, masterVolume: -1f, musicVolume: 1f, sfxVolume: 1f, uiVolume: 1f);

            Assert.That(overOne, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(negativeMaster, Is.EqualTo(0f).Within(Tolerance));
        }
    }
}
