using System;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Provides a friendly display name for UITransitionSettings types in the Inspector dropdown.
    /// </summary>

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TransitionSettingsNameAttribute : Attribute
    {
        public string Name { get; }

        public TransitionSettingsNameAttribute(string name)
        {
            Name = name;
        }
    }
}


