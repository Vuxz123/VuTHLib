using NUnit.Framework;

namespace _VuTH.Core.Audio.Tests.EditMode
{
    public class AudioSettingsBindingTests
    {
        private int _changeCount;
        private AudioSettingsBinding _binding;

        [SetUp]
        public void SetUp()
        {
            _changeCount = 0;
            _binding = new AudioSettingsBinding(() => _changeCount++);
        }

        [TearDown]
        public void TearDown()
        {
            _binding.DisposeSubscriptions();
            _binding.ReleasePackage();
        }

        [Test]
        public void Bind_SetsPackageAndFlags_AndNotifiesOncePerSetting()
        {
            var package = new AudioSettingsPackage(false, 1f, 1f, 1f, 1f);

            _binding.Bind(package, ownsPackage: true, isRegistered: false);

            Assert.That(_binding.Package, Is.SameAs(package));
            Assert.That(_binding.OwnsPackage, Is.True);
            Assert.That(_binding.IsRegistered, Is.False);
            // R3 ReactiveProperty emits the current value on subscribe: 5 settings -> 5 notifications.
            Assert.That(_changeCount, Is.EqualTo(5));
        }

        [Test]
        public void SettingChange_InvokesCallback()
        {
            var package = new AudioSettingsPackage(false, 1f, 1f, 1f, 1f);
            _binding.Bind(package, ownsPackage: true, isRegistered: false);
            _changeCount = 0;

            package.MasterVolume.Value = 0.5f;
            package.Muted.Value = true;

            Assert.That(_changeCount, Is.EqualTo(2));
        }

        [Test]
        public void Bind_SamePackageAgain_OnlyUpdatesFlags()
        {
            var package = new AudioSettingsPackage(false, 1f, 1f, 1f, 1f);
            _binding.Bind(package, ownsPackage: true, isRegistered: false);
            _changeCount = 0;

            _binding.Bind(package, ownsPackage: false, isRegistered: true);

            Assert.That(_binding.OwnsPackage, Is.False);
            Assert.That(_binding.IsRegistered, Is.True);
            Assert.That(_changeCount, Is.EqualTo(0));

            package.SfxVolume.Value = 0.3f;
            Assert.That(_changeCount, Is.EqualTo(1), "Subscriptions must not be duplicated.");
        }

        [Test]
        public void Bind_NewPackage_StopsListeningToPreviousPackage()
        {
            var first = new AudioSettingsPackage(false, 1f, 1f, 1f, 1f);
            var second = new AudioSettingsPackage(false, 1f, 1f, 1f, 1f);
            _binding.Bind(first, ownsPackage: false, isRegistered: false);
            _binding.Bind(second, ownsPackage: false, isRegistered: false);
            _changeCount = 0;

            first.UiVolume.Value = 0.2f;

            Assert.That(_binding.Package, Is.SameAs(second));
            Assert.That(_changeCount, Is.EqualTo(0));

            first.Dispose();
            second.Dispose();
        }

        [Test]
        public void ReleasePackage_ClearsPackageAndFlags()
        {
            var package = new AudioSettingsPackage(false, 1f, 1f, 1f, 1f);
            _binding.Bind(package, ownsPackage: true, isRegistered: false);

            _binding.DisposeSubscriptions();
            _binding.ReleasePackage();

            Assert.That(_binding.Package, Is.Null);
            Assert.That(_binding.OwnsPackage, Is.False);
            Assert.That(_binding.IsRegistered, Is.False);
        }

        [Test]
        public void TryRegisterOwnedPackage_WithoutOwnedPackage_ReturnsFalse()
        {
            Assert.That(_binding.TryRegisterOwnedPackage(), Is.False);
        }
    }
}
