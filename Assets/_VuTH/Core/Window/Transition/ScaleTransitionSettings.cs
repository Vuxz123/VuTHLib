using System;
using UnityEngine;

namespace Core.Window.Transition
{
    [Serializable]
    [TransitionSettingsName("Scale")]
    public sealed class ScaleTransitionSettings : UITransitionSettings
    {
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public override IUITransition Create()
        {
            return new ScaleTransition(duration, scaleCurve);
        }
    }
}
