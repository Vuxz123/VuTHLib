using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace _VuTH.Common.Tests.PlayMode
{
    /// <summary>
    /// Manager lifecycle: default root registration injects the manager, init / deinit are paired and idempotent,
    /// and destroying a manager deinitializes it.
    /// </summary>
    public class ManagerLifecycleTests
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

            foreach (var m in Object.FindObjectsByType<ToggleTestManager>(FindObjectsInactive.Include))
            {
                if (m) Object.DestroyImmediate(m.gameObject);
            }
        }

        private static GateTestManager CreateBootstrap() =>
            new GameObject("GateTestManager").AddComponent<GateTestManager>();

        private static ToggleTestManager CreateToggle(bool enabled, bool customLifecycle)
        {
            // Inactive while configuring: Awake runs on activation, with the serialized flags set.
            var go = new GameObject("ToggleTestManager");
            go.SetActive(false);
            var manager = go.AddComponent<ToggleTestManager>();
            manager.Configure(enabled, customLifecycle);
            go.SetActive(true);
            return manager;
        }

        // ---- DI registration ----

        [Test]
        public void DefaultRootRegistration_InjectsTheManager()
        {
            // ScreenManager / CameraManager relied on this default, which used RegisterInstance: never injected.
            var manager = CreateBootstrap();

            var builder = new ContainerBuilder();
            builder.Register<GateTestDependency>(Lifetime.Singleton);
            manager.ConfigureRootScope(builder);
            using var container = builder.Build();

            Assert.IsTrue(manager.Injected, "[Inject] Construct must run for the default registration");
        }

        [Test]
        public void DefaultRootRegistration_ExposesSelfInterfaceAndBaseInterfaces()
        {
            var manager = CreateBootstrap();

            var builder = new ContainerBuilder();
            builder.Register<GateTestDependency>(Lifetime.Singleton);
            manager.ConfigureRootScope(builder);
            using var container = builder.Build();

            Assert.AreSame(manager, container.Resolve<GateTestManager>(), "as self");
            Assert.AreSame(manager, container.Resolve<IGateTestManager>(), "as its interface");
            Assert.AreSame(manager, container.Resolve<IGateTestService>(), "as the interfaces it extends");
            Assert.IsFalse(container.TryResolve<ICommonManager>(out _), "not as the generic manager contract");
        }

        // ---- Bootstrap lifecycle ----

        [Test]
        public void Destroy_DeinitializesTheInitializedManager_Once()
        {
            var manager = CreateBootstrap();
            Assert.IsTrue(manager.IsInitialized);

            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(1, manager.DeinitializeCount, "cleanup used to never run for bootstrap managers");
        }

        [UnityTest]
        public IEnumerator Duplicate_IsNeitherInitializedNorDeinitialized()
        {
            var primary = CreateBootstrap();
            var duplicate = CreateBootstrap();

            yield return null; // the duplicate is destroyed at the end of the frame

            Assert.IsFalse(duplicate);
            Assert.AreEqual(0, duplicate.InitializeCount);
            Assert.AreEqual(0, duplicate.DeinitializeCount);
            Assert.AreEqual(0, primary.DeinitializeCount, "the primary is untouched");
            Assert.IsTrue(primary.IsInitialized);
        }

        [Test]
        public void ThrowingDeinitialize_IsLogged_AndDoesNotEscapeOnDestroy()
        {
            var manager = CreateBootstrap();
            manager.ThrowOnDeinitialize = true;

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: deinit failure (expected by the test)");
            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(1, manager.DeinitializeCount);
        }

        [UnityTest]
        public IEnumerator DuplicateOnAnObjectWithOtherComponents_OnlyRemovesTheManagerComponent()
        {
            var primary = CreateBootstrap();
            var other = new GameObject("Shared", typeof(BoxCollider));
            var duplicate = other.AddComponent<GateTestManager>();

            yield return null;

            Assert.IsTrue(other, "the GameObject (and its other components) must survive");
            Assert.IsNotNull(other.GetComponent<BoxCollider>());
            Assert.IsFalse(duplicate);
            Assert.IsTrue(primary);
            Object.DestroyImmediate(other);
        }

        // ---- Toggleable managers ----

        [Test]
        public void Toggleable_EnabledInitializesInAwake_DisabledDoesNot()
        {
            var enabled = CreateToggle(enabled: true, customLifecycle: false);
            Assert.AreEqual(1, enabled.InitializeCount);
            Assert.IsTrue(enabled.IsInitialized);
            Object.DestroyImmediate(enabled.gameObject);

            var disabled = CreateToggle(enabled: false, customLifecycle: false);
            Assert.AreEqual(0, disabled.InitializeCount);
            Assert.IsFalse(disabled.IsInitialized);
        }

        [Test]
        public void Toggleable_EnableAndDisable_ArePairedAndIdempotent()
        {
            var manager = CreateToggle(enabled: false, customLifecycle: false);

            manager.EnableSystem(true);
            manager.EnableSystem(true);
            Assert.AreEqual(1, manager.InitializeCount);

            manager.EnableSystem(false);
            manager.EnableSystem(false);
            Assert.AreEqual(1, manager.DeinitializeCount);
            Assert.IsFalse(manager.IsInitialized);

            manager.ToggleSystem();
            Assert.AreEqual(2, manager.InitializeCount);
            Assert.IsTrue(manager.IsEnabledSystem);
        }

        [Test]
        public void Toggleable_CustomLifecycle_StartsOnEnableSystemTrue_EvenIfFlagAlreadySet()
        {
            // enableSystem already true + custom lifecycle: EnableSystem(true) used to return early and never init.
            var manager = CreateToggle(enabled: true, customLifecycle: true);
            Assert.AreEqual(0, manager.InitializeCount, "custom lifecycle: no init in Awake");

            manager.EnableSystem(true);

            Assert.AreEqual(1, manager.InitializeCount);
        }

        [Test]
        public void Toggleable_DestroyWhileDisabled_DoesNotDeinitialize()
        {
            var manager = CreateToggle(enabled: true, customLifecycle: false);
            manager.EnableSystem(false);

            Object.DestroyImmediate(manager.gameObject);

            Assert.AreEqual(1, manager.DeinitializeCount, "only the EnableSystem(false) one");
        }
    }
}
