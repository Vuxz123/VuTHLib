using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using VContainer;

namespace _VuTH.Common.DI
{
    /// <summary>
    /// Extension point that lets other modules contribute registrations to every root / scene scope without
    /// Common.DI depending on them (e.g. Common.MessagePipe registers its brokers here instead of the scopes
    /// calling MessagePipeHelper directly).
    /// <para>
    /// Register from a <c>[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]</c> method. Registration is keyed and
    /// idempotent (re-registering a key replaces it), so it is safe with Enter Play Mode without domain reload.
    /// Installers run before the scope's configurators, in key order.
    /// </para>
    /// </summary>
    public static class ScopeInstallers
    {
        private static readonly Dictionary<string, Action<IContainerBuilder>> RootInstallers = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Action<IContainerBuilder, Scene>> SceneInstallers = new(StringComparer.Ordinal);

        public static void RegisterRoot(string key, Action<IContainerBuilder> install)
        {
            Register(RootInstallers, key, install);
        }

        /// <summary>Scene installers receive the scope's OWN scene (not the active scene: screens load additively).</summary>
        public static void RegisterScene(string key, Action<IContainerBuilder, Scene> install)
        {
            Register(SceneInstallers, key, install);
        }

        public static bool UnregisterRoot(string key) => RootInstallers.Remove(key);

        public static bool UnregisterScene(string key) => SceneInstallers.Remove(key);

        internal static IReadOnlyCollection<string> RootKeys => RootInstallers.Keys;

        internal static IReadOnlyCollection<string> SceneKeys => SceneInstallers.Keys;

        internal static void InstallRoot(IContainerBuilder builder) => Install(RootInstallers, builder);

        internal static void InstallScene(IContainerBuilder builder, Scene scene)
        {
            foreach (var pair in SceneInstallers.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                pair.Value(builder, scene);
            }
        }

        private static void Register<T>(Dictionary<string, T> map, string key, T install) where T : Delegate
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Installer key must not be empty.", nameof(key));
            map[key] = install ?? throw new ArgumentNullException(nameof(install));
        }

        private static void Install(Dictionary<string, Action<IContainerBuilder>> map, IContainerBuilder builder)
        {
            foreach (var pair in map.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                pair.Value(builder);
            }
        }
    }
}
