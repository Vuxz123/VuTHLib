using System;

namespace Common.Editor.Settings
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SettingsTabAttribute : Attribute
    {
        public readonly string Id;
        public readonly string Title;
        public readonly int Order;

        public SettingsTabAttribute(
            string id,
            string title,
            int order = 0)
        {
            Id = id;
            Title = title;
            Order = order;
        }
    }
}