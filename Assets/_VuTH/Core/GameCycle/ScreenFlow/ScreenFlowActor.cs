using System;
using Common.SharedLib.Log;
using Core.GameCycle.Screen;

namespace Core.GameCycle.ScreenFlow
{
    /// <summary>
    /// Handles intents (Trigger) and side-effects (calling ScreenManager). Coalesces triggers while transitioning.
    /// </summary>
    public sealed class ScreenFlowActor : IDisposable
    {
        private readonly ScreenFlowStateContainer _state;
        private readonly IScreenFlowResolver _resolver;
        private readonly TransitionContext _transitionContext;
        
        private IScreenManager _navigator;

        private string _pendingEvent;
        private bool _started;
        private bool _disposed;

        public ScreenFlowActor(
            ScreenFlowStateContainer state,
            IScreenFlowResolver resolver,
            TransitionContext transitionContext)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _transitionContext = transitionContext;

            if (!LazyInitScreenManager())
            {
                this.LogWarning("Lazy setup screen manager call when it not even been initialized.");
            }

            // Initialize state from start node if needed.
            if (_state.CurrentNode == null)
            {
                _state.Reset(_resolver.GetStartNode());
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _navigator.OnTransitionCompleted -= HandleTransitionCompleted;
        }

        private bool LazyInitScreenManager()
        {
            if (!ScreenManager.HasInstance)
            {
                return false;
            }
            if (_navigator == null)
            {
                _navigator = ScreenManager.Instance;
                _navigator.OnTransitionCompleted += HandleTransitionCompleted;
            }
            return true;
        }

        public void StartFlow()
        {
            if (_disposed) return;
            
            if (!LazyInitScreenManager())
            {
                this.LogWarning("Lazy setup screen manager call when it not even been initialized.");
                return;
            }

            var screenFlowNode = _resolver.GetStartNode();
            if (screenFlowNode == null)
            {
                this.LogWarning("ScreenFlowActor: No start/current node. Ignored trigger.");
                return;
            }

            if (_started)
            {
                this.LogWarning("ScreenFlowActor: Screen is already started.");
                return;
            }
            
            _started = true;

            var screenModel = screenFlowNode.Screen;
            if (!screenModel)
            {
                this.LogWarning("ScreenFlowActor: Screen node is not screen.");
                return;
            }
            
            _navigator.Enter(screenModel, _transitionContext);
        }

        public void Trigger(string eventName)
        {
            if (_disposed) return;
            
            if (!LazyInitScreenManager())
            {
                this.LogWarning("Lazy setup screen manager call when it not even been initialized.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(eventName))
                return;

            // Mandatory policy: coalesce ONLY when ScreenManager is transitioning.
            if (_navigator.IsTransitioning)
            {
                _pendingEvent = eventName;
                return;
            }

            var currentNode = _state.CurrentNode;
            if (currentNode == null)
            {
                currentNode = _resolver.GetStartNode();
                _state.Reset(currentNode);
            }

            if (currentNode == null)
            {
                this.LogWarning("ScreenFlowActor: No start/current node. Ignored trigger.");
                return;
            }

            if (!_resolver.TryResolve(currentNode, eventName, out var nextNode) || nextNode == null)
                return;

            // Update state first? We'll update state before side-effect to be 'source of truth'.
            _state.Set(nextNode, eventName);

            // Deterministic navigation policy: Enter for every resolved transition.
            var target = nextNode.Screen;
            if (target == null)
            {
                this.LogWarning($"ScreenFlowActor: Resolved node '{nextNode.Guid}' has null Screen.");
                return;
            }

            // Fire-and-forget. ScreenManager guards concurrency via IsTransitioning.
            _navigator.Enter(target, _transitionContext);
        }

        private void HandleTransitionCompleted(TransitionCompletedEventArgs args)
        {
            if (_disposed) return;

            // Drain only AFTER an actual transition completes.
            if (string.IsNullOrWhiteSpace(_pendingEvent)) return;

            var e = _pendingEvent;
            _pendingEvent = null;

            Trigger(e);
        }
    }
}
