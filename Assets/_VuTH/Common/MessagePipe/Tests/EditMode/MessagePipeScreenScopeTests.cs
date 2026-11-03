#if VCONTAINER
using System;
using System.Linq;
using System.Text.RegularExpressions;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using _VuTH.Common.MessagePipe.Editor;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace _VuTH.Common.MessagePipe.Tests.EditMode
{
    [MessagePipeEvent(EventScope.Screen, "Arena")]
    public sealed class TestArenaScreenEvent { }

    [MessagePipeEvent(EventScope.Screen)]
    public sealed class TestScreenEventWithoutId { }

    /// <summary>Screen scope, misuse guard (resolving a scoped event outside its scopes) and target validation.</summary>
    public class MessagePipeScreenScopeTests
    {
        private static MessagePipeEventInfo Info<T>() =>
            MessagePipeEventCatalog.Scan(new[] { typeof(MessagePipeScreenScopeTests).Assembly }).Single(e => e.Type == typeof(T));

        // ---- screen scope ----

        [Test]
        public void ScreenEvent_AppliesToScopesOfThatScreen_NotToASceneWithTheSameName()
        {
            var info = Info<TestArenaScreenEvent>();

            Assert.AreEqual("Arena", info.ScreenId);
            Assert.IsTrue(info.AppliesTo("ArenaUI", new[] { "Arena" }), "any scene of the screen (main or additive)");
            Assert.IsFalse(info.AppliesTo("Arena", Array.Empty<string>()), "a scene name is not a screen id");
            Assert.IsFalse(info.AppliesTo("ArenaUI", new[] { "Home" }));
        }

        [Test]
        public void ScreenEvent_IsIsolatedBetweenScreens_AndSharedInsideOne()
        {
            var root = BuildRoot(ScopedEventMisuse.Ignore);
            using var arenaMain = root.CreateScope(b => MessagePipeHelper.RegisterSceneEvents(b, "ArenaMain", new[] { "Arena" }));
            using var homeMain = root.CreateScope(b => MessagePipeHelper.RegisterSceneEvents(b, "HomeMain", new[] { "Home" }));

            var arenaReceived = 0;
            var homeReceived = 0;
            using var a = arenaMain.Resolve<ISubscriber<TestArenaScreenEvent>>().Subscribe(_ => arenaReceived++);
            using var h = homeMain.Resolve<ISubscriber<TestArenaScreenEvent>>().Subscribe(_ => homeReceived++);

            arenaMain.Resolve<IPublisher<TestArenaScreenEvent>>().Publish(new TestArenaScreenEvent());

            Assert.AreEqual(1, arenaReceived);
            Assert.AreEqual(0, homeReceived, "the Home screen falls back to the global broker, not Arena's");
            root.Dispose();
        }

        // ---- misuse guard ----

        private static IObjectResolver BuildRoot(ScopedEventMisuse mode)
        {
            var builder = new ContainerBuilder();
            MessagePipeHelper.RegisterGlobalEvents(builder, mode);
            return builder.Build();
        }

        [Test]
        public void Warn_ResolvingScopedEventOutsideItsScope_LogsOncePerEventType_AndStillWorks()
        {
            using var root = BuildRoot(ScopedEventMisuse.Warn);

            LogAssert.Expect(LogType.Warning, new Regex("IPublisher<TestArenaScreenEvent> was resolved outside its owning scope"));
            var publisher = root.Resolve<IPublisher<TestArenaScreenEvent>>();
            root.Resolve<IPublisher<TestArenaScreenEvent>>(); // singleton: no second warning

            var received = 0;
            // Same event type: already reported once, no second warning for the subscriber.
            using var sub = root.Resolve<ISubscriber<TestArenaScreenEvent>>().Subscribe(_ => received++);
            publisher.Publish(new TestArenaScreenEvent());

            Assert.AreEqual(1, received, "falls back to the shared global broker");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Error_ResolvingScopedEventOutsideItsScope_Throws()
        {
            using var root = BuildRoot(ScopedEventMisuse.Error);

            var e = Assert.Catch(() => root.Resolve<ISubscriber<TestAnySceneEvent>>());
            StringAssert.Contains("outside its owning scope", e.ToString());
        }

        [Test]
        public void OwningScope_GetsItsOwnBroker_WithoutAnyWarning()
        {
            using var root = BuildRoot(ScopedEventMisuse.Error);
            using var arena = root.CreateScope(b => MessagePipeHelper.RegisterSceneEvents(b, "ArenaMain", new[] { "Arena" }));

            Assert.DoesNotThrow(() => arena.Resolve<IPublisher<TestArenaScreenEvent>>());
            Assert.DoesNotThrow(() => arena.Resolve<IAsyncSubscriber<TestAnySceneEvent>>());
        }

        [Test]
        public void GlobalEvents_AreNeverGuarded()
        {
            using var root = BuildRoot(ScopedEventMisuse.Error);

            Assert.DoesNotThrow(() => root.Resolve<IPublisher<TestGlobalEvent>>());
            Assert.DoesNotThrow(() => root.Resolve<IPublisher<TestUnmarkedMessage>>());
        }

        // ---- validation ----

        [Test]
        public void Validator_ReportsUnknownScenesAndScreens_AndMissingScreenId()
        {
            var events = MessagePipeEventCatalog.Scan(new[] { typeof(MessagePipeScreenScopeTests).Assembly });

            var issues = MessagePipeTargetValidator.Validate(events,
                knownSceneNames: new[] { "Home" },          // "Arena" scene missing
                knownScreenIds: new[] { "Home" });          // "Arena" screen missing

            Assert.IsTrue(issues.Any(i => !i.IsError && i.EventType == typeof(TestArenaEvent)), "unknown scene");
            Assert.IsTrue(issues.Any(i => !i.IsError && i.EventType == typeof(TestArenaScreenEvent)), "unknown screen");
            Assert.IsTrue(issues.Any(i => i.IsError && i.EventType == typeof(TestScreenEventWithoutId)), "screen id required");
            Assert.IsFalse(issues.Any(i => i.EventType == typeof(TestAnySceneEvent)), "empty scene name = every scene, valid");
        }

        [Test]
        public void Validator_KnownTargets_AreValid()
        {
            var events = MessagePipeEventCatalog.Scan(new[] { typeof(MessagePipeScreenScopeTests).Assembly })
                .Where(e => e.Type != typeof(TestScreenEventWithoutId));

            var issues = MessagePipeTargetValidator.Validate(events, new[] { "Arena" }, new[] { "Arena" });

            CollectionAssert.IsEmpty(issues);
        }
    }
}
#endif
