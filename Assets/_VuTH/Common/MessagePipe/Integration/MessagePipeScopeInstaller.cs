#if VCONTAINER
using _VuTH.Common.DI;
using UnityEngine;

namespace _VuTH.Common.MessagePipe
{
    /// <summary>
    /// Plugs MessagePipe into every DI scope through <see cref="ScopeInstallers"/>: global brokers in the root scope,
    /// scene-scoped brokers in each scene scope. (Common.DI used to call MessagePipeHelper directly, which made the DI
    /// layer depend on the event layer.)
    /// </summary>
    internal static class MessagePipeScopeInstaller
    {
        private const string Key = "VuTH.Common.MessagePipe";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            ScopeInstallers.RegisterRoot(Key, MessagePipeHelper.RegisterGlobalEvents);
            ScopeInstallers.RegisterScene(Key, MessagePipeHelper.RegisterSceneEvents);
        }
    }
}
#endif
