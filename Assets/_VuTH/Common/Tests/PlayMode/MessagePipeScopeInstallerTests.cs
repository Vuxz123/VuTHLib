using _VuTH.Common.DI;
using NUnit.Framework;

namespace _VuTH.Common.Tests.PlayMode
{
    /// <summary>
    /// After entering Play Mode, Common.MessagePipe must have plugged its brokers into both scope kinds
    /// (it registers itself at SubsystemRegistration — DI no longer calls it directly).
    /// </summary>
    public class MessagePipeScopeInstallerTests
    {
        [Test]
        public void MessagePipe_IsRegisteredAsRootAndSceneInstaller()
        {
            CollectionAssert.Contains(ScopeInstallers.RootKeys, "VuTH.Common.MessagePipe");
            CollectionAssert.Contains(ScopeInstallers.SceneKeys, "VuTH.Common.MessagePipe");
        }
    }
}
