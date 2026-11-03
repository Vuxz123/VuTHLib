using System;
using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Transition;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.GameCycle.ScreenFlow
{
    /// <summary>
    /// Handles intents (<see cref="Trigger"/>) and their side-effect (navigating the <see cref="IScreenManager"/>).
    /// <list type="bullet">
    /// <item>One navigation at a time. Events triggered while a transition runs are queued (FIFO) and each is resolved
    /// against the node that is current when it is processed.</item>
    /// <item>Queued events that match no transition are dropped and the next one is tried immediately, so a
    /// non-matching event can no longer block the queue until some later transition.</item>
    /// <item>The actor awaits its own navigation, so the queue keeps draining (and the error is logged) even when a
    /// transition throws.</item>
    /// <item>Flow state only advances once the resolved node is known to have a screen.</item>
    /// </list>
    /// </summary>
    public sealed class ScreenFlowActor : IDisposable
    {
        private readonly ScreenFlowStateContainer _state;
        private readonly IScreenFlowResolver _resolver;
        private readonly TransitionContext _transitionContext;
        private readonly Func<IScreenManager> _navigatorProvider;

        private IScreenManager _navigator;
        private readonly Queue<string> _pendingEvents = new();
        private bool _navigating;
        private bool _started;
        private bool _disposed;

        /// <param name="navigatorProvider">
        /// Returns the screen manager to drive, or null while it is not available yet.
        /// Defaults to the <see cref="ScreenManager"/> singleton.
        /// </param>
        public ScreenFlowActor(
            ScreenFlowStateContainer state,
            IScreenFlowResolver resolver,
            TransitionContext transitionContext,
            Func<IScreenManager> navigatorProvider = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _transitionContext = transitionContext;
            _navigatorProvider = navigatorProvider ?? DefaultNavigator;

            if (_state.CurrentNode == null)
            {
                _state.Reset(_resolver.GetStartNode());
            }
        }

        public bool IsStarted => _started;

        /// <summary>Number of triggered events waiting for the current transition to finish.</summary>
        public int PendingEventCount => _pendingEvents.Count;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pendingEvents.Clear();

            if (_navigator != null)
            {
                _navigator.OnTransitionCompleted -= HandleTransitionCompleted;
                _navigator = null;
            }
        }

        /// <summary>Navigates to the start node's screen. Can be retried if it fails before navigating.</summary>
        public void StartFlow()
        {
            if (_disposed) return;

            if (_started)
            {
                this.LogWarning("ScreenFlowActor: Screen is already started.");
                return;
            }

            if (!TryGetNavigator(out var navigator))
            {
                this.LogWarning("ScreenFlowActor: StartFlow called before the ScreenManager is available.");
                return;
            }

            var startNode = _resolver.GetStartNode();
            if (startNode == null)
            {
                this.LogWarning("ScreenFlowActor: Graph has no start node. Flow not started.");
                return;
            }

            if (!startNode.Screen)
            {
                this.LogWarning($"ScreenFlowActor: Start node '{startNode.Guid}' has no screen. Flow not started.");
                return;
            }

            _started = true;
            Navigate(navigator, startNode.Screen, transition: null);
        }

        public void Trigger(string eventName)
        {
            if (_disposed) return;
            if (string.IsNullOrWhiteSpace(eventName)) return;

            if (!TryGetNavigator(out _))
            {
                this.LogWarning($"ScreenFlowActor: Trigger('{eventName}') ignored, the ScreenManager is not available.");
                return;
            }

            if (!_resolver.HasEvent(eventName))
            {
                // Not used by any transition in the graph: almost always a typo. Use the generated
                // ScreenFlowEvents constants (Screen Flow settings tab) to catch these at compile time.
                this.LogWarning($"ScreenFlowActor: event '{eventName}' does not exist in the ScreenFlow graph.");
                return;
            }

            _pendingEvents.Enqueue(eventName);
            ProcessPending();
        }

        private void ProcessPending()
        {
            if (_disposed || _navigating) return;
            if (!TryGetNavigator(out var navigator)) return;

            // Someone else (game code calling ScreenManager directly) is transitioning: wait for its completion event.
            if (navigator.IsTransitioning) return;

            while (_pendingEvents.Count > 0)
            {
                var eventName = _pendingEvents.Dequeue();
                if (TryResolveAndNavigate(navigator, eventName)) return; // one navigation at a time
            }
        }

        private bool TryResolveAndNavigate(IScreenManager navigator, string eventName)
        {
            var currentNode = _state.CurrentNode;
            if (currentNode == null)
            {
                currentNode = _resolver.GetStartNode();
                _state.Reset(currentNode);
            }

            if (currentNode == null)
            {
                this.LogWarning($"ScreenFlowActor: No start/current node. Ignored trigger '{eventName}'.");
                return false;
            }

            if (!_resolver.TryResolveTransition(currentNode, eventName, out var transition, out var nextNode) ||
                nextNode == null)
            {
                this.LogVerbose($"ScreenFlowActor: '{eventName}' has no transition from node '{currentNode.Guid}'.");
                return false;
            }

            var target = nextNode.Screen;
            if (!target)
            {
                this.LogWarning($"ScreenFlowActor: Resolved node '{nextNode.Guid}' has no screen. Flow state unchanged.");
                return false;
            }

            _state.Set(nextNode, eventName);
            Navigate(navigator, target, transition);
            return true;
        }

        private void Navigate(IScreenManager navigator, ScreenModel target, ScreenFlowTransition transition)
        {
            RunNavigation(navigator, target, transition).Forget();
        }

        private async UniTaskVoid RunNavigation(IScreenManager navigator, ScreenModel target, ScreenFlowTransition transition)
        {
            _navigating = true;
            try
            {
                await Execute(navigator, target, transition);
            }
            catch (Exception e)
            {
                this.LogError($"ScreenFlowActor: navigation to '{target.name}' failed: {e}");
            }
            finally
            {
                _navigating = false;
                ProcessPending();
            }
        }

        /// <summary>Maps the transition's navigation mode onto the ScreenManager API (start / null = Enter).</summary>
        private UniTask Execute(IScreenManager navigator, ScreenModel target, ScreenFlowTransition transition)
        {
            var mode = transition?.Navigation ?? ScreenFlowNavigation.Enter;
            switch (mode)
            {
                case ScreenFlowNavigation.Push:
                    var hideBelow = transition.ScreenBelow switch
                    {
                        ScreenBelowVisibility.Hide => true,
                        ScreenBelowVisibility.KeepVisible => false,
                        _ => target.hideScreenBelow,
                    };
                    return navigator.Push(target, hideBelow, _transitionContext);

                case ScreenFlowNavigation.PushOverride:
                    return navigator.PushOverride(target, _transitionContext);

                case ScreenFlowNavigation.Pop:
                    if (navigator.CanPop && !navigator.IsOverriding) return navigator.Pop(_transitionContext);
                    this.LogWarning($"ScreenFlowActor: Pop to '{target.name}' requested but there is nothing to pop. Entering it instead.");
                    return navigator.Enter(target, _transitionContext);

                case ScreenFlowNavigation.PopOverride:
                    if (navigator.IsOverriding) return navigator.PopOverride(_transitionContext);
                    this.LogWarning($"ScreenFlowActor: PopOverride to '{target.name}' requested but no override is active. Entering it instead.");
                    return navigator.Enter(target, _transitionContext);

                default:
                    return navigator.Enter(target, _transitionContext);
            }
        }

        private void HandleTransitionCompleted(TransitionCompletedEventArgs args)
        {
            ProcessPending();
        }

        private bool TryGetNavigator(out IScreenManager navigator)
        {
            if (_navigator != null && !_navigator.IsAlive())
            {
                // The ScreenManager we subscribed to was destroyed (scene reload, domain without reload...).
                _navigator = null;
            }

            if (_navigator == null)
            {
                var candidate = _navigatorProvider();
                if (candidate.IsAlive())
                {
                    _navigator = candidate;
                    _navigator.OnTransitionCompleted += HandleTransitionCompleted;
                }
            }

            navigator = _navigator;
            return navigator != null;
        }

        private static IScreenManager DefaultNavigator()
        {
            return ScreenManager.HasInstance ? ScreenManager.Instance : null;
        }
    }
}
