using System;
using UnityEngine;

namespace _VuTH.Common.Scene
{
    /// <summary>
    /// Draws a <see cref="string"/> field as a dropdown of the enabled scenes in Build Settings; the field stores the
    /// scene name. A value that is no longer in Build Settings is shown as "(Missing!)" and can be cleared with a click.
    /// Used by <see cref="SceneField"/>; can also be put on any string field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SceneSelectorAttribute : PropertyAttribute
    {
    }
}
