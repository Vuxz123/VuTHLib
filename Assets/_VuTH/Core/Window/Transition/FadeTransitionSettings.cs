using System;

namespace _VuTH.Core.Window.Transition
{
    [Serializable]
    [TransitionSettingsName("Fade")]
    public sealed class FadeTransitionSettings : UITransitionSettings
    {
        public override IUITransition Create()
        {
            return new FadeTransition(duration);
        }
    }
}

