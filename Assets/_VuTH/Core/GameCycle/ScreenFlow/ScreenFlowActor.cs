using System;
using System.Collections.Generic;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen;
using _VuTH.Core.GameCycle.Screen.Core.A;
using _VuTH.Core.GameCycle.Screen.Transition;

namespace _VuTH.Core.GameCycle.ScreenFlow
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

        private readonly Queue<string> _pendingEvents = new();
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

            if (_navigator.IsTransitioning)
            {
                _pendingEvents.Enqueue(eventName);
                return;
            }

            var visitedNodes = new HashSet<string>();
            ProcessTrigger(eventName, visitedNodes);
        }

        private void ProcessTrigger(string eventName, HashSet<string> visitedNodes)
        {
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

            if (!visitedNodes.Add(currentNode.Guid))
            {
                this.LogError($"[ScreenFlowActor] Circular loop detected! Node '{currentNode.Guid}' already visited in this trigger chain. Aborting transition.");
                return;
            }

            if (!_resolver.TryResolve(currentNode, eventName, out var nextNode) || nextNode == null)
                return;

            _state.Set(nextNode, eventName);

            var target = nextNode.Screen;
            if (target == null)
            {
                this.LogWarning($"ScreenFlowActor: Resolved node '{nextNode.Guid}' has null Screen.");
                return;
            }

            _navigator.Enter(target, _transitionContext);
        }

        private void HandleTransitionCompleted(TransitionCompletedEventArgs args)
        {
            if (_disposed) return;

            if (_pendingEvents.Count == 0) return;

            var nextEvent = _pendingEvents.Dequeue();
            Trigger(nextEvent);
        }
    }
}
