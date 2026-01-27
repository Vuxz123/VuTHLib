using System;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Polymorphic transition settings stored inline via SerializeReference.
    /// Derived types are edited in Inspector via a custom drawer.
    /// </summary>
    [Serializable]
    public abstract class UITransitionSettings
    {
        public float duration = 0.3f;

        public abstract IUITransition Create();
    }
}

