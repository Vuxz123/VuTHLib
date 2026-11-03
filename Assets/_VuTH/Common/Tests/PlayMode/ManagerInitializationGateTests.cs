using System.Collections;
using _VuTH.Common.DI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace _VuTH.Common.Tests.PlayMode
{
    /// <summary>
    /// Bootstrap managers created during root DI configuration must initialize AFTER injection, and duplicate
    /// singletons must neither initialize nor register.
    /// </summary>
    public class ManagerInitializationGateTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyAll();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAll();
            yield return null;
        }

        private static void DestroyAll()
        {
            foreach (var m in Object.FindObjectsByType<GateTestManager>(FindObjectsInactive.Include))
            {
                if (m) Object.DestroyImmediate(m.gameObject);
            }
        }

        private static GateTestManager Create() => new GameObject("GateTestManager").AddComponent<GateTestManager>();

        [Test]
        public void WithoutDeferral_InitializesInAwake_AsBefore()
        {
            var manager = Create();

            Assert.AreEqual(1, manager.InitializeCount);
            Assert.IsFalse(manager.Injected);
        }

        [Test]
        public void CreatedDuringDeferral_InitializesOnlyAfterTheContainerInjectedIt()
        {
            GateTestManager manager;
            using (VManagerInitializationGate.BeginDeferral())
            {
                manager = Create();
            }

            Assert.AreEqual(0, manager.InitializeCount, "Awake must not initialize while deferring");

            var builder = new ContainerBuilder();
            builder.Register<GateTestDependency>(Lifetime.Singleton);
            manager.ConfigureRootScope(builder);
            builder.RegisterBuildCallback(_ => VManagerInitializationGate.FlushDeferred()); // as BootstrapManagerCentral does
            using var container = builder.Build();

            Assert.AreEqual(1, manager.InitializeCount);
            Assert.IsTrue(manager.InjectedBeforeInitialize, "[Inject] Construct must run before InitializeBootstrap");
        }

        [Test]
        public void DuplicateSingleton_NeverInitializes_AndOptsOutOfRootRegistration()
        {
            var primary = Create();
            var duplicate = Create();

            Assert.IsTrue(primary.IsPrimaryInstance);
            Assert.IsFalse(duplicate.IsPrimaryInstance);
            Assert.AreEqual(0, duplicate.InitializeCount, "a duplicate is destroyed; it must not initialize first");
            Assert.IsFalse(((IRootScopeRegistrationFilter)duplicate).ShouldConfigureRootScope);
            Assert.IsTrue(((IRootScopeRegistrationFilter)primary).ShouldConfigureRootScope);
        }

        [Test]
        public void FlushDeferred_SkipsManagersDestroyedMeanwhile()
        {
            GateTestManager manager;
            using (VManagerInitializationGate.BeginDeferral())
            {
                manager = Create();
            }

            Object.DestroyImmediate(manager.gameObject);

            Assert.DoesNotThrow(VManagerInitializationGate.FlushDeferred);
        }
    }
}
