using System;

namespace _VuTH.Core.Window.Transition.Workers
{
    /// <summary>
    /// Base for workers that can handle multiple presets or parse preset strings.
    /// Example: "Slide:Left" / "ScaleFast".
    /// </summary>
    public abstract class ComplexPresetTransitionWorkerBase : UITransitionWorkerBase
    {
        public sealed override bool CanHandle(string presetName)
        {
            return !string.IsNullOrWhiteSpace(presetName) && CanHandleInternal(presetName.Trim());
        }

        public sealed override IUITransition Create(string presetName)
        {
            return string.IsNullOrWhiteSpace(presetName) ? null : CreateInternal(presetName.Trim());
        }

        protected abstract bool CanHandleInternal(string presetName);
        protected abstract IUITransition CreateInternal(string presetName);

        protected static bool StartsWith(string value, string prefix)
        {
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}

