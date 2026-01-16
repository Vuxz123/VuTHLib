using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Common.Editor.Settings
{
    [InitializeOnLoad]
    public static class SettingsRegistry
    {
        private static readonly List<ISettingsTab> InternalTabs = new();
        private static bool _initialized;

        static SettingsRegistry()
        {
            Rebuild();
        }

        public static IReadOnlyList<ISettingsTab> Tabs => InternalTabs;

        private static void Rebuild()
        {
            InternalTabs.Clear();

            foreach (var type in TypeCache.GetTypesWithAttribute<SettingsTabAttribute>())
            {
                if (!typeof(ISettingsTab).IsAssignableFrom(type))
                {
                    Debug.LogError(
                        $"[ScreenFlow] {type.FullName} has ScreenFlowSettingsTabAttribute " +
                        $"but does not implement IScreenFlowSettingsTab");
                    continue;
                }

                if (type.IsAbstract)
                    continue;

                var attr = type.GetCustomAttribute<SettingsTabAttribute>();

                var tab = (ISettingsTab)Activator.CreateInstance(type);

                InternalTabs.Add(tab);
            }

            InternalTabs.Sort((a, b) => a.Order.CompareTo(b.Order));
            _initialized = true;
        }
    }
}