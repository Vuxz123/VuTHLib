using System;
using UnityEngine;

namespace Core.Window.Transition
{
    [Serializable]
    [TransitionSettingsName("Slide")]
    public sealed class SlideTransitionSettings : UITransitionSettings
    {
        public SlideTransition.Direction direction = SlideTransition.Direction.Right;
        public float distance = 1000f;
        
        public override IUITransition Create()
        {
            // Note: SlideTransition currently doesn't take a curve.
            return new SlideTransition(direction, duration, distance);
        }
    }
}
