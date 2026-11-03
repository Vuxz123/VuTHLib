#if VCONTAINER
using System.Linq;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace _VuTH.Common.MessagePipe.Tests.EditMode
{
    [MessagePipeEvent]
    public sealed class TestGlobalEvent { }

    [MessagePipeEvent(EventScope.Scene)]
    public sealed class TestAnySceneEvent { }

    [MessagePipeEvent(EventScope.Scene, "Arena")]
    public sealed class TestArenaEvent { }

    [MessagePipeEvent]
    public abstract class TestAbstractEvent { }

    public sealed class TestUnmarkedMessage { }

    public class MessagePipeScopingTests
    {
        private static MessagePipeEventInfo Info<T>() =>
            MessagePipeEventCatalog.Scan(new[] { typeof(MessagePipeScopingTests).Assembly }).Single(e => e.Type == typeof(T));

        // ---- discovery ----

        [Test]
        public void Scan_FindsAttributedConcreteTypes_WithScopeAndScene()
        {
            var found = MessagePipeEventCatalog.Scan(new[] { typeof(MessagePipeScopingTests).Assembly });

            Assert.AreEqual(EventScope.Global, found.Single(e => e.Type == typeof(TestGlobalEvent)).Scope);
            Assert.AreEqual("", found.Single(e => e.Type == typeof(TestAnySceneEvent)).SceneName);
            Assert.AreEqual("Arena", found.Single(e => e.Type == typeof(TestArenaEvent)).SceneName);
            Assert.IsFalse(found.Any(e => e.Type == typeof(TestAbstractEvent)), "abstract types are skipped");
            Assert.IsFalse(found.Any(e => e.Type == typeof(TestUnmarkedMessage)));
        }

        [Test]
        public void AppliesToScene_MatchesTheScopesOwnSceneOrEveryScene()
        {
            Assert.IsTrue(Info<TestAnySceneEvent>().AppliesToScene("Home"));
            Assert.IsTrue(Info<TestArenaEvent>().AppliesToScene("Arena"));
            Assert.IsFalse(Info<TestArenaEvent>().AppliesToScene("Home"));
            Assert.IsFalse(Info<TestGlobalEvent>().AppliesToScene("Arena"), "global events never get scene brokers");
        }

        [Test]
        public void ProjectEventsAreDiscovered_WithoutAnyBakeOrWhitelist()
        {
            // These assemblies were missing from the old whitelist, so the bake skipped them.
            var names = MessagePipeEventCatalog.All.Select(e => e.Type.FullName).ToList();
            CollectionAssert.Contains(names, "_VuTH.Core.Audio.Events.MessagePipe.PlayAudioCueEvent");
            CollectionAssert.Contains(names, "_VuTH.Core.Persistent.SaveSystem.Events.SaveEvent");
        }

        // ---- container wiring ----

        private static IObjectResolver BuildRoot()
        {
            var builder = new ContainerBuilder();
            MessagePipeHelper.RegisterGlobalEvents(builder);
            return builder.Build();
        }

        [Test]
        public void Root_ResolvesBrokersForAnyType_IncludingUnmarkedOnes()
        {
            using var root = BuildRoot();

            Assert.DoesNotThrow(() => root.Resolve<IPublisher<TestUnmarkedMessage>>());
            Assert.DoesNotThrow(() => root.Resolve<ISubscriber<TestGlobalEvent>>());
        }

        [Test]
        public void SceneEvents_AreIsolatedPerSceneScope()
        {
            using var root = BuildRoot();
            using var home = root.CreateScope(b => MessagePipeHelper.RegisterSceneEvents(b, "Home"));
            using var shop = root.CreateScope(b => MessagePipeHelper.RegisterSceneEvents(b, "Shop"));

            var homeReceived = 0;
            var shopReceived = 0;
            using var homeSub = home.Resolve<ISubscriber<TestAnySceneEvent>>().Subscribe(_ => homeReceived++);
            using var shopSub = shop.Resolve<ISubscriber<TestAnySceneEvent>>().Subscribe(_ => shopReceived++);

            home.Resolve<IPublisher<TestAnySceneEvent>>().Publish(new TestAnySceneEvent());

            Assert.AreEqual(1, homeReceived);
            Assert.AreEqual(0, shopReceived, "a scene-scoped message must not leak into another scene");
        }

        [Test]
        public void NamedSceneEvent_OnlyGetsABrokerInThatScene()
        {
            int Ours(string scene) => MessagePipeEventCatalog.ForScene(scene)
                .Count(e => e.Type.Assembly == typeof(MessagePipeScopingTests).Assembly);

            Assert.AreEqual(2, Ours("Arena"), "any-scene event + Arena-only event");
            Assert.AreEqual(1, Ours("Home"), "only the any-scene event");
        }

        [Test]
        public void GlobalEvents_AreSharedAcrossScopes()
        {
            using var root = BuildRoot();
            using var home = root.CreateScope(b => MessagePipeHelper.RegisterSceneEvents(b, "Home"));

            var received = 0;
            using var sub = root.Resolve<ISubscriber<TestGlobalEvent>>().Subscribe(_ => received++);
            home.Resolve<IPublisher<TestGlobalEvent>>().Publish(new TestGlobalEvent());

            Assert.AreEqual(1, received);
        }

        // ---- options ----

        [Test]
        public void ConfiguredOptions_AreApplied()
        {
            var config = ScriptableObject.CreateInstance<MessagePipeOptionsConfig>();
            try
            {
                config.enableCaptureStackTrace = true;
                var options = MessagePipeHelper.ApplyConfiguredOptions(new MessagePipeOptions(), config);
                Assert.IsTrue(options.EnableCaptureStackTrace);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
#endif
