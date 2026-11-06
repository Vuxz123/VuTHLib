using _VuTH.Core.Window.Transition;
using NUnit.Framework;

namespace _VuTH.Core.Window.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for <see cref="WindowOptionsResolver"/>: unset fields take the window's defaults, then the
    /// built-in ones; set fields always keep the caller's value.
    /// </summary>
    [TestFixture]
    public class WindowOptionsResolverTests
    {
        private sealed class FakeDefinition : IWindowDefinition
        {
            public WindowType WindowType { get; set; } = WindowType.System;
            public string TransitionPreset { get; set; } = "Fade";
            public UITransitionSettings TransitionInSettings { get; set; }
            public UITransitionSettings TransitionOutSettings { get; set; }
            public bool BlockInput { get; set; }
            public bool CloseOnBackPress { get; set; }
            public WindowDuplicatePolicy DuplicatePolicy { get; set; } = WindowDuplicatePolicy.Ignore;
            public bool KeepOnScreenChange { get; set; } = true;
            public bool KeepInstance { get; set; } = true;
            public bool CloseOnOutsideClick { get; set; } = true;
        }

        [Test]
        public void ApplyDefaults_NullDefinition_LeavesOptionsUntouched()
        {
            var options = new WindowOptions();

            WindowOptionsResolver.ApplyDefaults(options, null);

            Assert.IsNull(options.WindowType);
            Assert.IsNull(options.TransitionPreset);
            Assert.IsNull(options.CloseOnBackPress);
            Assert.IsNull(options.BlockInput);
        }

        [Test]
        public void ApplyDefaults_NullOptions_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => WindowOptionsResolver.ApplyDefaults(null, new FakeDefinition()));
        }

        [Test]
        public void ApplyDefaults_UnsetWindowType_TakesDefinitionWindowType()
        {
            var options = new WindowOptions();

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { WindowType = WindowType.Tutorial });

            Assert.AreEqual(WindowType.Tutorial, options.WindowType);
        }

        [TestCase(WindowType.Popup)]
        [TestCase(WindowType.FullScreenPopup)]
        public void ApplyDefaults_SetWindowType_KeepsCallerValue_EvenTheBuiltInDefault(WindowType callerType)
        {
            // Popup used to be indistinguishable from "not set" and was overridden.
            var options = new WindowOptions { WindowType = callerType };

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { WindowType = WindowType.System });

            Assert.AreEqual(callerType, options.WindowType);
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

        [TestCase(null)]
        [TestCase("")]
        public void ApplyDefaults_UnsetPreset_TakesDefinitionPreset(string preset)
        {
            var options = new WindowOptions { TransitionPreset = preset };

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { TransitionPreset = "Slide" });

            Assert.AreEqual("Slide", options.TransitionPreset);
        }

        [TestCase("Custom")]
        [TestCase("Scale")]
        public void ApplyDefaults_SetPreset_KeepsCallerPreset(string preset)
        {
            var options = new WindowOptions { TransitionPreset = preset };

            WindowOptionsResolver.ApplyDefaults(options, new FakeDefinition { TransitionPreset = "Fade" });

            Assert.AreEqual(preset, options.TransitionPreset);
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

        [Test]
        public void Resolve_WithoutDefinition_FillsBuiltInDefaults()
        {
            var resolved = WindowOptionsResolver.Resolve(null, null);

            Assert.AreEqual(WindowType.Popup, resolved.WindowType);
            Assert.AreEqual(WindowOptions.DefaultTransitionPreset, resolved.TransitionPreset);
            Assert.AreEqual(true, resolved.CloseOnBackPress);
            Assert.AreEqual(true, resolved.BlockInput);
            Assert.AreEqual(WindowDuplicatePolicy.Allow, resolved.Duplicate);
            Assert.AreEqual(false, resolved.KeepOnScreenChange);
            Assert.AreEqual(false, resolved.KeepInstance);
        }

        [Test]
        public void Resolve_Duplicate_CallerThenDefinition()
        {
            Assert.AreEqual(WindowDuplicatePolicy.Ignore,
                WindowOptionsResolver.Resolve(null, new FakeDefinition()).Duplicate);
            Assert.AreEqual(WindowDuplicatePolicy.ReplaceExisting,
                WindowOptionsResolver.Resolve(new WindowOptions { Duplicate = WindowDuplicatePolicy.ReplaceExisting },
                    new FakeDefinition()).Duplicate);
        }

        [Test]
        public void Resolve_DoesNotChangeTheCallersOptions()
        {
            var options = new WindowOptions { Data = 1 };

            var resolved = WindowOptionsResolver.Resolve(options, new FakeDefinition());

            Assert.AreNotSame(options, resolved);
            Assert.IsNull(options.WindowType, "the caller may reuse its options for another window");
            Assert.AreEqual(WindowType.System, resolved.WindowType);
            Assert.AreEqual(1, resolved.Data);
        }
    }
}
