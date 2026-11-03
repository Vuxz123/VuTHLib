using System.Collections.Generic;
using _VuTH.Common.MessagePipe.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _VuTH.Core.GameCycle.Screen.Core
{
    /// <summary>
    /// Tells which screens a scene belongs to, for <c>[MessagePipeEvent(EventScope.Screen, ...)]</c>:
    /// <list type="number">
    /// <item>the screen ScreenManager is loading while the scene's scopes are being built (covers additive scenes
    /// without metadata);</item>
    /// <item>every <see cref="ScreenMetaData"/> on the scene's root objects.</item>
    /// </list>
    /// Plugged into <see cref="MessagePipeScopeContext.ScreenResolver"/> at startup.
    /// </summary>
    public static class ScreenSceneMembership
    {
        /// <summary>Screen currently loading its scenes (set by ScreenManager around scene loads).</summary>
        public static ScreenModel Loading { get; internal set; }

        public static IReadOnlyCollection<string> GetScreenIds(Scene scene)
        {
            var ids = new List<string>(2);

            if (Loading && Loading.screenId)
            {
                ids.Add(Loading.screenId.name);
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (!root || !root.TryGetComponent<ScreenMetaData>(out var meta)) continue;
                    if (!meta.model || !meta.model.screenId) continue;

                    var id = meta.model.screenId.name;
                    if (!ids.Contains(id)) ids.Add(id);
                }
            }

            return ids;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            Loading = null;
            MessagePipeScopeContext.ScreenResolver = GetScreenIds;
        }
    }
}
