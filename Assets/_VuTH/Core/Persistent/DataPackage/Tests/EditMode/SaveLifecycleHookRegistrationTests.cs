#if VCONTAINER
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace _VuTH.Core.Persistent.DataPackage.Tests.EditMode
{
    /// <summary>
    /// The hook only works if VContainer calls Initialize() on it. It used to be registered with a plain
    /// Register&lt;SaveLifecycleHook&gt;(), which VContainer never initializes — so no pause / quit flush ever ran.
    /// This builds a real container with the production registration.
    /// </summary>
    public class SaveLifecycleHookRegistrationTests
    {
        private const string RelayName = "[VuTH] ApplicationPauseRelay";

        private static bool RelayExists() =>
            Resources.FindObjectsOfTypeAll<GameObject>().Any(go => go != null && go.name == RelayName);

        [Test]
        public void BuildingTheContainer_InitializesTheHook_AndDisposingItCleansUp()
        {
            var builder = new ContainerBuilder();
            DataPersistenceManager.RegisterLifecycleHook(builder);

            var container = builder.Build();
            try
            {
                Assert.IsTrue(RelayExists(), "Initialize() must run when the container is built");
                Assert.IsNotNull(container.Resolve<SaveLifecycleHook>(), "still injectable as its concrete type");
            }
            finally
            {
                container.Dispose();
            }

            Assert.IsFalse(RelayExists(), "Dispose() must run with the container");
        }
    }
}
#endif
