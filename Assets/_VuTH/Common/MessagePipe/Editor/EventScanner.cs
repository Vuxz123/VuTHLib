using System;
using System.Collections.Generic;
using System.Reflection;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using UnityEngine;
using ZLinq;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Handles scanning assemblies for MessagePipe event types.
    /// </summary>
    internal static class EventScanner
    {
        /// <summary>
        /// Scan all whitelisted assemblies for types with [MessagePipeEvent] attribute.
        /// </summary>
        public static List<EventScopeEntry> ScanAssembliesForEvents(MessagePipeAssemblyWhitelist whitelist)
        {
            var entries = new List<EventScopeEntry>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            int totalTypes = 0;
            int eligibleTypes = 0;
            int withAttributeCount = 0;
            int withoutAttributeCount = 0;

            foreach (var assembly in assemblies)
            {
                if (!IsWhitelisted(assembly, whitelist))
                    continue;

                var assemblyName = assembly.GetName().Name;
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        ProcessTypeForEvent(entries, type, ref totalTypes, ref eligibleTypes, ref withAttributeCount, ref withoutAttributeCount);
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"[MessagePipe Baker] Failed to load some types from {assemblyName}: {ex.Message}");
                    // Process loadable types
                    foreach (var type in ex.Types.AsValueEnumerable().Where(t => t != null))
                    {
                        ProcessTypeForEvent(entries, type, ref totalTypes, ref eligibleTypes, ref withAttributeCount, ref withoutAttributeCount);
                    }
                }
            }

            // Sort for deterministic output
            entries.Sort((a, b) => string.Compare(a.typeFullName, b.typeFullName, StringComparison.Ordinal));

            Debug.Log($"[MessagePipe Baker] Scan summary: totalTypes={totalTypes}, eligibleTypes={eligibleTypes}, " +
                      $"withAttribute={withAttributeCount}, withoutAttribute={withoutAttributeCount}, entries={entries.Count}");

            return entries;
        }

        private static void ProcessTypeForEvent(List<EventScopeEntry> entries, Type type,
            ref int totalTypes, ref int eligibleTypes, ref int withAttributeCount, ref int withoutAttributeCount)
        {
            totalTypes++;

            // Validate type is concrete (not abstract or generic)
            if ((!type.IsValueType && !type.IsClass) || type.IsAbstract || type.ContainsGenericParameters)
                return;

            eligibleTypes++;

            var attr = type.GetCustomAttribute<MessagePipeEventAttribute>();
            if (attr == null)
            {
                // Not a MessagePipe event type
                withoutAttributeCount++;
                return;
            }

            withAttributeCount++;

            var scope = attr.Scope;
            var sceneName = attr.SceneName ?? string.Empty;
            var registerAsyncBroker = attr.RegisterAsyncBroker;

            // Validate Scene-scoped events have a non-empty SceneName
            if (scope == EventScope.Scene && string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[MessagePipe Baker] Event {type.FullName} is marked as Scene scope but has no SceneName specified. " +
                               "Please provide a SceneName in the [MessagePipeEvent] attribute, e.g., [MessagePipeEvent(EventScope.Scene, \"MyScene\")].");
                // Continue baking with empty sceneName; registration will skip or warn at runtime
            }

            entries.Add(new EventScopeEntry
            {
                typeFullName = type.AssemblyQualifiedName,
                scope = scope,
                sceneName = sceneName ?? string.Empty,
                registerAsyncBroker = registerAsyncBroker
            });
        }

        /// <summary>
        /// Check if an assembly is whitelisted via whitelist asset or marker attribute.
        /// </summary>
        public static bool IsWhitelisted(Assembly assembly, MessagePipeAssemblyWhitelist whitelist)
        {
            var assemblyName = assembly.GetName().Name;
            if (whitelist.Contains(assemblyName))
                return true;

            return assembly.GetCustomAttribute<VuTHMessagePipeEventAssemblyAttribute>() != null;
        }
    }
}
