using _VuTH.Core.Window.Transition;
using NUnit.Framework;

namespace _VuTH.Core.Window.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for <see cref="WindowOptionsResolver"/> (merge of caller options with definition defaults).
    /// </summary>
    [TestFixture]
    public class WindowOptionsResolverTests
    {
        private sealed class FakeDefinition : IWindowDefinition
        {
            public WindowType WindowType { get; set; } = WindowType.System;
            public UILayer Layer { get; set; } = UILayer.System;
            public string TransitionPreset { get; set; } = "Fade";
            public UITransitionSettings TransitionInSettings { get; set; }
            public UITransitionSettings TransitionOutSettings { get; set; }
            public bool BlockInput { get; set; }
            public bool CloseOnBackPress { get; set; }
        }

        [Test]
        public void ApplyDefaults_NullDefinition_LeavesOptionsUntouched()
        {
            var options = new WindowOptions();

            WindowOptionsResolver.ApplyDefaults(options, null);

            Assert.AreEqual(WindowType.Popup, options.WindowType);
            Assert.AreEqual("Scale", options.TransitionPreset);
            Assert.IsNull(options.CloseOnBackPress);
            Assert.IsNull(options.BlockInput);
        }

        [Test]
        public void ApplyDefaults_NullOptions_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => WindowOptionsResolver.ApplyDefaults(null, new FakeDefinition()));
        }

        [Test]
        public void ApplyDefaults_DefaultWindowType_TakesDefinitionWindowType()
        {
            var options = new WindowOptions();

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { WindowType = WindowType.Tutorial });

            Assert.AreEqual(WindowType.Tutorial, options.WindowType);
        }

        [Test]
        public void ApplyDefaults_CustomWindowType_KeepsCallerValue()
        {
            var options = new WindowOptions { WindowType = WindowType.FullScreenPopup };

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { WindowType = WindowType.System });

            Assert.AreEqual(WindowType.FullScreenPopup, options.WindowType);
        }

        [Test]
        public void ApplyDefaults_NullTransitionSettings_TakeDefinitionSettings()
        {
            var defIn = new FadeTransitionSettings();
            var defOut = new FadeTransitionSettings();
            var options = new WindowOptions();

            WindowOptionsResolver.ApplyDefaults(options,
                new FakeDefinition { TransitionInSettings = defIn, TransitionOutSettings = defOut });

            Assert.AreSame(defIn, options.TransitionInSettings);
            Assert.AreSame(defOut, options.TransitionOutSettings);
        }

        [Test]
        public void ApplyDefaults_CallerTransitionSettings_AreKept()
        {
            var callerIn = new FadeTransitionSettings();
            var options = new WindowOptions { TransitionInSettings = callerIn };

            WindowOptionsResolver.ApplyDefaults(options,
                new FakeDefinition { TransitionInSettings = new FadeTransitionSettings() });

            Assert.AreSame(callerIn, options.TransitionInSettings);
        }

        [Test]
        public void ApplyDefaults_DefaultPresetAndNoSettings_TakesDefinitionPreset()
        {
            var options = new WindowOptions();

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { TransitionPreset = "Slide" });

            Assert.AreEqual("Slide", options.TransitionPreset);
        }

        [Test]
        public void ApplyDefaults_EmptyPresetAndNoSettings_TakesDefinitionPreset()
        {
            var options = new WindowOptions { TransitionPreset = string.Empty };

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { TransitionPreset = "Fade" });

            Assert.AreEqual("Fade", options.TransitionPreset);
        }

        [Test]
        public void ApplyDefaults_CustomPreset_KeepsCallerPreset()
        {
            var options = new WindowOptions { TransitionPreset = "Custom" };

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { TransitionPreset = "Fade" });

            Assert.AreEqual("Custom", options.TransitionPreset);
        }

        [Test]
        public void ApplyDefaults_AnyTransitionSettingsResolved_KeepsDefaultPreset()
        {
            var options = new WindowOptions();

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition
            {
                TransitionPreset = "Fade",
                TransitionOutSettings = new FadeTransitionSettings()
            });

            Assert.AreEqual("Scale", options.TransitionPreset);
        }

        [Test]
        public void ApplyDefaults_UnspecifiedBools_TakeDefinitionValues()
        {
            var options = new WindowOptions();

            WindowOptionsResolver.ApplyDefaults(options,
                new FakeDefinition { CloseOnBackPress = false, BlockInput = true });

            Assert.AreEqual(false, options.CloseOnBackPress);
            Assert.AreEqual(true, options.BlockInput);
        }

        [Test]
        public void ApplyDefaults_SpecifiedBools_KeepCallerValues()
        {
            var options = new WindowOptions { CloseOnBackPress = true, BlockInput = false };

            WindowOptionsResolver.ApplyDefaults(options,
                new FakeDefinition { CloseOnBackPress = false, BlockInput = true });

            Assert.AreEqual(true, options.CloseOnBackPress);
            Assert.AreEqual(false, options.BlockInput);
        }
    }
}
