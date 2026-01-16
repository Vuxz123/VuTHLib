using System;
using JetBrains.Annotations;

namespace Common.Editor.Settings
{
    [MeansImplicitUse, AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SettingsTabAttribute : Attribute
    {
    }
}