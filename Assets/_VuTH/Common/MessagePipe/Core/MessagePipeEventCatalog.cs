using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _VuTH.Common.MessagePipe.Attributes;

namespace _VuTH.Common.MessagePipe.Core
{
    /// <summary>A type marked with <see cref="MessagePipeEventAttribute"/>.</summary>
    public sealed class MessagePipeEventInfo
    {
        public MessagePipeEventInfo(Type type, EventScope scope, string target)
        {
            Type = type;
            Scope = scope;
            Target = target ?? string.Empty;
        }

        public Type Type { get; }
        public EventScope Scope { get; }

        /// <summary>Scene name (Scene scope, empty = every scene) or ScreenIdentifier name (Screen scope).</summary>
        public string Target { get; }

        /// <summary>Scene-scoped events only: the scene that owns the broker, or empty = every scene gets its own.</summary>
        public string SceneName => Scope == EventScope.Scene ? Target : string.Empty;

        /// <summary>Screen-scoped events only: the ScreenIdentifier name.</summary>
        public string ScreenId => Scope == EventScope.Screen ? Target : string.Empty;

        /// <summary>True for Scene / Screen events (they must not be used through the global broker).</summary>
        public bool IsScoped => Scope != EventScope.Global;

        /// <summary>True when a scene scope of <paramref name="sceneName"/> must register its own broker for this event.</summary>
        public bool AppliesToScene(string sceneName)
        {
            return AppliesTo(sceneName, Array.Empty<string>());
        }

        /// <summary>
        /// True when a scope living in <paramref name="sceneName"/>, which belongs to the screens
        /// <paramref name="screenIds"/>, must register its own broker for this event.
        /// </summary>
        public bool AppliesTo(string sceneName, IReadOnlyCollection<string> screenIds)
        {
            switch (Scope)
            {
                case EventScope.Scene:
                    return Target.Length == 0 || string.Equals(Target, sceneName, StringComparison.Ordinal);
                case EventScope.Screen:
                    if (Target.Length == 0 || screenIds == null) return false;
                    foreach (var id in screenIds)
                    {
                        if (string.Equals(id, Target, StringComparison.Ordinal)) return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Runtime discovery of <see cref="MessagePipeEventAttribute"/> types — replaces the old editor "bake"
    /// (whitelist + EventScopeLookup asset + generated registrar), which went stale and duplicated what MessagePipe
    /// already does: with VContainer ≥ 1.14 <c>RegisterMessagePipe()</c> registers open-generic brokers, so global
    /// events need no per-type registration at all. Only scene-scoped events (and the non-VContainer fallback) need
    /// the list.
    /// <para>
    /// Scans once (lazily, cached): only assemblies that reference this one can declare the attribute.
    /// </para>
    /// </summary>
    public static class MessagePipeEventCatalog
    {
        private static IReadOnlyList<MessagePipeEventInfo> _all;

        public static IReadOnlyList<MessagePipeEventInfo> All => _all ??= Scan(AppDomain.CurrentDomain.GetAssemblies());

        /// <summary>Scene-scoped events a scope in <paramref name="sceneName"/> registers its own brokers for.</summary>
        public static IEnumerable<MessagePipeEventInfo> ForScene(string sceneName)
        {
            return ForScope(sceneName, Array.Empty<string>());
        }

        /// <summary>Scene + Screen events a scope in <paramref name="sceneName"/> (part of <paramref name="screenIds"/>) owns.</summary>
        public static IEnumerable<MessagePipeEventInfo> ForScope(string sceneName, IReadOnlyCollection<string> screenIds)
        {
            return All.Where(e => e.AppliesTo(sceneName, screenIds));
        }

        /// <summary>Drops the cache (e.g. after an editor recompile; a domain reload resets it anyway).</summary>
        public static void Invalidate()
        {
            _all = null;
        }

        /// <summary>Finds attributed event types in <paramref name="assemblies"/> (sorted by full name).</summary>
        public static IReadOnlyList<MessagePipeEventInfo> Scan(IEnumerable<Assembly> assemblies)
        {
            var self = typeof(MessagePipeEventAttribute).Assembly;
            var selfName = self.GetName().Name;
            var result = new List<MessagePipeEventInfo>();

            foreach (var assembly in assemblies)
            {
                if (assembly == null || assembly.IsDynamic) continue;
                if (assembly != self && !References(assembly, selfName)) continue;

                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (type == null || type.IsAbstract || type.ContainsGenericParameters) continue;
                    if (!type.IsClass && !type.IsValueType) continue;

                    var attribute = type.GetCustomAttribute<MessagePipeEventAttribute>(inherit: false);
                    if (attribute == null) continue;

                    result.Add(new MessagePipeEventInfo(type, attribute.Scope, attribute.SceneName));
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
            return result;
        }

        private static bool References(Assembly assembly, string assemblyName)
        {
            try
            {
                return assembly.GetReferencedAssemblies().Any(r => r.Name == assemblyName);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }
}
