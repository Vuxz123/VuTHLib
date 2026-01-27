using System;
using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Marks a SerializeReference field as a transition settings reference so a custom inspector
    /// can offer a type dropdown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TransitionSettingsReferenceAttribute : PropertyAttribute
    {
    }
}

