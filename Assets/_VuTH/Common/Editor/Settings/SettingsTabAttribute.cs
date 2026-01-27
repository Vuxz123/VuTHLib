using System;
using JetBrains.Annotations;

namespace _VuTH.Common.Editor.Settings
{
    [MeansImplicitUse, AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SettingsTabAttribute : Attribute
    {
    }
}