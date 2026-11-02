using System;
using _VuTH.Core.GameCycle.Screen.Events.Global;
using _VuTH.Core.GameCycle.Screen.Events.Local;
using _VuTH.Core.GameCycle.Screen.Events.MessagePipe;
using _VuTH.Core.GameCycle.Screen.Transition;
using MessagePipe;

namespace _VuTH.Core.GameCycle.Screen.Core
{
    /// <summary>
    /// Fans screen lifecycle events out to MessagePipe publishers, the inspector-assigned
    /// <see cref="GlobalScreenEventProfile"/>s and the local (current-screen) event notifier.
    /// For every global event the MessagePipe message is published first, then each non-null profile is notified in order.
    /// </summary>
    public sealed class ScreenEventDispatcher
    {
        private readonly Func<GlobalScreenEventProfile[]> _profilesProvider;
        private readonly ILocalScreenEventNotifier _localNotifier;

        /// <param name="profilesProvider">Returns the current global profiles (may return null). Read on every dispatch.</param>
        /// <param name="localNotifier">Receives local opening/closing notifications.</param>
        public ScreenEventDispatcher(Func<GlobalScreenEventProfile[]> profilesProvider, ILocalScreenEventNotifier localNotifier)
        {
            _profilesProvider = profilesProvider ?? throw new ArgumentNullException(nameof(profilesProvider));
            _localNotifier = localNotifier ?? throw new ArgumentNullException(nameof(localNotifier));
        }

        #region MessagePipe publishers (optional; null = not published)

        public IPublisher<PreScreenEnterEvent> PreEnterPublisher { get; set; }
        public IPublisher<PostScreenEnterEvent> PostEnterPublisher { get; set; }
        public IPublisher<PreScreenExitEvent> PreExitPublisher { get; set; }
        public IPublisher<PostScreenExitEvent> PostExitPublisher { get; set; }
        public IPublisher<ScreenTransitionCompletedEvent> TransitionCompletedPublisher { get; set; }

        #endregion

        #region Global

        public void NotifyGlobalPreExit(ScreenEventArgs args)
        {
            PublishMessagePipeEvent(PreExitPublisher, new PreScreenExitEvent(args.FromScreen, args.ToScreen));
            var profiles = _profilesProvider();
            if (profiles == null) return;
            foreach (var t in profiles)
            {
                if (t != null) t.NotifyPreScreenExit(args);
            }
        }

        public void NotifyGlobalPostExit(ScreenEventArgs args)
        {
            PublishMessagePipeEvent(PostExitPublisher, new PostScreenExitEvent(args.FromScreen, args.ToScreen));
            var profiles = _profilesProvider();
            if (profiles == null) return;
            foreach (var t in profiles)
            {
                if (t != null) t.NotifyPostScreenExit(args);
            }
        }

        public void NotifyGlobalPreEnter(ScreenEventArgs args)
        {
            PublishMessagePipeEvent(PreEnterPublisher, new PreScreenEnterEvent(args.FromScreen, args.ToScreen));
            var profiles = _profilesProvider();
            if (profiles == null) return;
            foreach (var t in profiles)
            {
                if (t != null) t.NotifyPreScreenEnter(args);
            }
        }

        public void NotifyGlobalPostEnter(ScreenEventArgs args)
        {
            PublishMessagePipeEvent(PostEnterPublisher, new PostScreenEnterEvent(args.FromScreen, args.ToScreen));
            var profiles = _profilesProvider();
            if (profiles == null) return;
            foreach (var t in profiles)
            {
                if (t != null) t.NotifyPostScreenEnter(args);
            }
        }

        #endregion

        #region Local

        public void NotifyLocalClosing(ScreenEventArgs args)
        {
            _localNotifier.NotifyScreenClosing(args);
        }

        public void NotifyLocalOpening(ScreenEventArgs args)
        {
            _localNotifier.NotifyScreenOpening(args);
        }

        #endregion

        /// <summary>
        /// Publishes <see cref="ScreenTransitionCompletedEvent"/> via MessagePipe, then invokes <paramref name="callback"/>
        /// with a <see cref="TransitionCompletedEventArgs"/> (from/to are passed as <see cref="ScreenModel"/>, or null).
        /// </summary>
        public void NotifyTransitionCompleted(TransitionKind kind, IScreenDefinition from, IScreenDefinition to,
            TransitionContext context, Action<TransitionCompletedEventArgs> callback)
        {
            PublishMessagePipeEvent(TransitionCompletedPublisher, new ScreenTransitionCompletedEvent(kind, from, to, context));
            // Note: from/to may be null during initial bootstrap, handle gracefully
            var fromScreen = from as ScreenModel;
            var toScreen = to as ScreenModel;
            callback?.Invoke(new TransitionCompletedEventArgs(kind, fromScreen, toScreen, context));
        }

        private static void PublishMessagePipeEvent<T>(IPublisher<T> publisher, T eventArgs) where T : class
        {
            publisher?.Publish(eventArgs);
        }
    }
}
