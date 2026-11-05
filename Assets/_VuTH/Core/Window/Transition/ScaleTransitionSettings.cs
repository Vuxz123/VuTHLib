using System;
using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    [Serializable]
    [TransitionSettingsName("Scale")]
    public sealed class ScaleTransitionSettings : UITransitionSettings
    {
        [Tooltip("Scale curve (0..1). Leave empty for the default OutBack / InBack eases.")]
        public AnimationCurve scaleCurve = new();

        public override IUITransition Create()
        {
            return new ScaleTransition(duration, scaleCurve);
        }
    }
}
