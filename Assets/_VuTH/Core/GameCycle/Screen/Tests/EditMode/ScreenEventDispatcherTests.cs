using System;
using System.Collections.Generic;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Events.Global;
using _VuTH.Core.GameCycle.Screen.Events.Local;
using _VuTH.Core.GameCycle.Screen.Events.MessagePipe;
using _VuTH.Core.GameCycle.Screen.Identifier;
using _VuTH.Core.GameCycle.Screen.Transition;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _VuTH.Core.GameCycle.Screen.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ScreenEventDispatcher"/>: fan-out order and null tolerance.
    /// </summary>
    public sealed class ScreenEventDispatcherTests
    {
        private readonly List<string> _log = new();

        private GlobalScreenEventProfile _profile1;
        private GlobalScreenEventProfile _profile2;
        private ScreenModel _from;
        private ScreenModel _to;
        private LocalScreenEventContainer _local;

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _profile1 = ScriptableObject.CreateInstance<GlobalScreenEventProfile>();
            _profile2 = ScriptableObject.CreateInstance<GlobalScreenEventProfile>();
            _profile1.RegisterListener(new RecordingListener("p1", _log));
            _profile2.RegisterListener(new RecordingListener("p2", _log));

            _from = ScriptableObject.CreateInstance<ScreenModel>();
            _to = ScriptableObject.CreateInstance<ScreenModel>();

            _local = new LocalScreenEventContainer();
        }

        [TearDown]
        public void TearDown()
        {
            if (_profile1) Object.DestroyImmediate(_profile1);
            if (_profile2) Object.DestroyImmediate(_profile2);
            if (_from) Object.DestroyImmediate(_from);
            if (_to) Object.DestroyImmediate(_to);
        }

        private ScreenEventDispatcher CreateDispatcher(GlobalScreenEventProfile[] profiles)
        {
            return new ScreenEventDispatcher(() => profiles, _local)
            {
                PreEnterPublisher = new RecordingPublisher<PreScreenEnterEvent>("mp:preEnter", _log),
                PostEnterPublisher = new RecordingPublisher<PostScreenEnterEvent>("mp:postEnter", _log),
                PreExitPublisher = new RecordingPublisher<PreScreenExitEvent>("mp:preExit", _log),
                PostExitPublisher = new RecordingPublisher<PostScreenExitEvent>("mp:postExit", _log),
                TransitionCompletedPublisher = new RecordingPublisher<ScreenTransitionCompletedEvent>("mp:completed", _log),
            };
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ScreenEventDispatcher(null, _local));
            Assert.Throws<ArgumentNullException>(() => new ScreenEventDispatcher(() => null, null));
        }

        [Test]
        public void GlobalEvents_PublishMessagePipeFirst_ThenProfilesInOrder()
        {
            var dispatcher = CreateDispatcher(new[] { _profile1, _profile2 });
            var args = new ScreenEventArgs(_from, _to);

            dispatcher.NotifyGlobalPreExit(args);
            dispatcher.NotifyGlobalPostExit(args);
            dispatcher.NotifyGlobalPreEnter(args);
            dispatcher.NotifyGlobalPostEnter(args);

            CollectionAssert.AreEqual(new[]
            {
                "mp:preExit", "p1:preExit", "p2:preExit",
                "mp:postExit", "p1:postExit", "p2:postExit",
                "mp:preEnter", "p1:preEnter", "p2:preEnter",
                "mp:postEnter", "p1:postEnter", "p2:postEnter",
            }, _log);
        }

        [Test]
        public void GlobalEvents_PassFromAndToThrough()
        {
            IScreenDefinition publishedFrom = null;
            IScreenDefinition publishedTo = null;
            var dispatcher = new ScreenEventDispatcher(() => null, _local)
            {
                PreEnterPublisher = new CallbackPublisher<PreScreenEnterEvent>(e =>
                {
                    publishedFrom = e.FromScreen;
                    publishedTo = e.ToScreen;
                }),
            };

            dispatcher.NotifyGlobalPreEnter(new ScreenEventArgs(_from, _to));

            Assert.AreSame(_from, publishedFrom);
            Assert.AreSame(_to, publishedTo);
        }

        [Test]
        public void GlobalEvents_NullProfilesArrayAndNullEntries_AreSkipped()
        {
            var args = new ScreenEventArgs(_from, _to);

            var noProfiles = CreateDispatcher(null);
            Assert.DoesNotThrow(() => noProfiles.NotifyGlobalPreEnter(args));
            CollectionAssert.AreEqual(new[] { "mp:preEnter" }, _log);

            _log.Clear();
            var withNullEntry = CreateDispatcher(new[] { null, _profile2 });
            withNullEntry.NotifyGlobalPostExit(args);
            CollectionAssert.AreEqual(new[] { "mp:postExit", "p2:postExit" }, _log);
        }

        [Test]
        public void GlobalEvents_NullPublishers_StillNotifyProfiles()
        {
            var dispatcher = new ScreenEventDispatcher(() => new[] { _profile1 }, _local);

            Assert.DoesNotThrow(() => dispatcher.NotifyGlobalPreExit(new ScreenEventArgs(_from, _to)));
            CollectionAssert.AreEqual(new[] { "p1:preExit" }, _log);
        }

        [Test]
        public void GlobalEvents_ProfilesProviderIsReadOnEveryDispatch()
        {
            var profiles = new[] { _profile1 };
            var dispatcher = new ScreenEventDispatcher(() => profiles, _local);
            var args = new ScreenEventArgs(_from, _to);

            dispatcher.NotifyGlobalPreEnter(args);
            profiles = new[] { _profile2 };
            dispatcher.NotifyGlobalPreEnter(args);

            CollectionAssert.AreEqual(new[] { "p1:preEnter", "p2:preEnter" }, _log);
        }

        [Test]
        public void LocalEvents_AreRoutedToLocalNotifier()
        {
            var dispatcher = CreateDispatcher(new[] { _profile1 });
            _local.RegisterOnScreenClosing(a => _log.Add("local:closing"));
            _local.RegisterOnScreenOpening(a => _log.Add("local:opening"));
            var args = new ScreenEventArgs(_from, _to);

            dispatcher.NotifyLocalClosing(args);
            dispatcher.NotifyLocalOpening(args);

            CollectionAssert.AreEqual(new[] { "local:closing", "local:opening" }, _log);
        }

        [Test]
        public void TransitionCompleted_PublishesThenInvokesCallback_WithScreenModels()
        {
            var dispatcher = CreateDispatcher(null);
            var context = new TransitionContext("test", "reason");
            TransitionCompletedEventArgs received = default;

            dispatcher.NotifyTransitionCompleted(TransitionKind.Push, _from, _to, context, a =>
            {
                _log.Add("callback");
                received = a;
            });

            CollectionAssert.AreEqual(new[] { "mp:completed", "callback" }, _log);
            Assert.AreEqual(TransitionKind.Push, received.Kind);
            Assert.AreSame(_from, received.From);
            Assert.AreSame(_to, received.To);
            Assert.AreEqual("test", received.Context.Source);
            Assert.AreEqual("reason", received.Context.Reason);
        }

        [Test]
        public void TransitionCompleted_NonScreenModelDefinitions_BecomeNullInCallback_ButArePublished()
        {
            ScreenTransitionCompletedEvent published = null;
            var dispatcher = new ScreenEventDispatcher(() => null, _local)
            {
                TransitionCompletedPublisher = new CallbackPublisher<ScreenTransitionCompletedEvent>(e => published = e),
            };
            var fakeFrom = new FakeDefinition();
            var fakeTo = new FakeDefinition();
            TransitionCompletedEventArgs received = default;

            dispatcher.NotifyTransitionCompleted(TransitionKind.Enter, fakeFrom, fakeTo, TransitionContext.Default, a => received = a);

            Assert.IsNotNull(published);
            Assert.AreSame(fakeFrom, published.FromScreen);
            Assert.AreSame(fakeTo, published.ToScreen);
            Assert.IsNull(received.From);
            Assert.IsNull(received.To);
        }

        [Test]
        public void TransitionCompleted_NullCallback_DoesNotThrow()
        {
            var dispatcher = CreateDispatcher(null);

            Assert.DoesNotThrow(() =>
                dispatcher.NotifyTransitionCompleted(TransitionKind.Pop, null, null, TransitionContext.Default, null));
            CollectionAssert.AreEqual(new[] { "mp:completed" }, _log);
        }

        #region Fakes

        private sealed class RecordingPublisher<T> : IPublisher<T>
        {
            private readonly string _tag;
            private readonly List<string> _log;

            public RecordingPublisher(string tag, List<string> log)
            {
                _tag = tag;
                _log = log;
            }

            public void Publish(T message) => _log.Add(_tag);
        }

        private sealed class CallbackPublisher<T> : IPublisher<T>
        {
            private readonly Action<T> _onPublish;

            public CallbackPublisher(Action<T> onPublish)
            {
                _onPublish = onPublish;
            }

            public void Publish(T message) => _onPublish(message);
        }

        private sealed class RecordingListener : IScreenEventListener
        {
            private readonly string _tag;
            private readonly List<string> _log;

            public RecordingListener(string tag, List<string> log)
            {
                _tag = tag;
                _log = log;
            }

            public void OnPreScreenEnter(ScreenEventArgs eventArgs) => _log.Add(_tag + ":preEnter");
            public void OnPostScreenEnter(ScreenEventArgs eventArgs) => _log.Add(_tag + ":postEnter");
            public void OnPreScreenExit(ScreenEventArgs eventArgs) => _log.Add(_tag + ":preExit");
            public void OnPostScreenExit(ScreenEventArgs eventArgs) => _log.Add(_tag + ":postExit");
        }

        private sealed class FakeDefinition : IScreenDefinition
        {
            public ScreenIdentifier ScreenID => null;
        }

        #endregion
    }
}
