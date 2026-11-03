using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace _VuTH.Common.MessagePipe.Core
{
    /// <summary>
    /// Answers "which screens does this scene belong to?" for <see cref="Attributes.EventScope.Screen"/> events.
    /// Common.MessagePipe cannot reference the Screen module, so the Screen module plugs a resolver in here
    /// (ScreenManager knows which screen it is loading; scenes also carry a ScreenMetaData).
    /// </summary>
    public static class MessagePipeScopeContext
    {
        /// <summary>Returns the ScreenIdentifier names the scene belongs to (empty when unknown).</summary>
        public static Func<Scene, IReadOnlyCollection<string>> ScreenResolver { get; set; }

        public static IReadOnlyCollection<string> ResolveScreens(Scene scene)
        {
            try
            {
                return ScreenResolver?.Invoke(scene) ?? Array.Empty<string>();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return Array.Empty<string>();
            }
        }
    }
}
