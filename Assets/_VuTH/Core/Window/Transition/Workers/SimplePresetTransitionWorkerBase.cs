using System;

namespace Core.Window.Transition.Workers
{
    /// <summary>
    /// Base for simple preset workers that handle a single preset (name match).
    /// </summary>
    public abstract class SimplePresetTransitionWorkerBase : UITransitionWorkerBase
    {
        protected abstract string Preset { get; }

        public override bool CanHandle(string presetName)
        {
            return !string.IsNullOrWhiteSpace(presetName)
                   && presetName.Equals(Preset, StringComparison.OrdinalIgnoreCase);
        }

        public sealed override IUITransition Create(string presetName)
        {
            return CreateInternal();
        }

        protected abstract IUITransition CreateInternal();
    }
}

