using System;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Transition;
using _VuTH.Core.GameCycle.ScreenFlow.Profile;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _VuTH.Core.GameCycle.ScreenFlow
{
    /// <summary>
    /// Public facade: wraps StateContainer + Actor. External callers should only depend on IScreenFlowManager.
    /// </summary>
    public sealed class ScreenFlowManager : VBootstrapManager<ScreenFlowManager, IScreenFlowManager> , IScreenFlowManager
    {
        [Header("Screen Flow")]
        [SerializeField, ReadOnlyField] private ScreenFlowGraph graph;
        
        [Header("Settings")]
        [SerializeField] private int historyCapacity = 32;
        
        private ScreenFlowStateContainer _state;
        private ScreenFlowActor _actor;
        private IScreenFlowResolver _graphResolver;
        
#if VCONTAINER
        private IObjectResolver _resolver;

        /// <summary>Stores the container; the screen manager is resolved when the flow navigates.</summary>
        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }
#endif
        
        protected override void InitializeBootstrap()
        {
            SetupGraph();
            _state = new ScreenFlowStateContainer(historyCapacity);
            _graphResolver = new ScreenFlowGraphResolver(graph);

            // init state
            _state.Reset(_graphResolver.GetStartNode());

            _actor = new ScreenFlowActor(
                _state,
                _graphResolver,
                new TransitionContext("screenflow"),
                ResolveScreenManager);
        }

        protected override void DeinitializeBootstrap()
        {
            _actor?.Dispose();
            _actor = null;
        }

        /// <summary>The injected screen manager when available, otherwise the singleton (setups without DI).</summary>
        private IScreenManager ResolveScreenManager()
        {
#if VCONTAINER
            if (_resolver != null && _resolver.TryResolve<IScreenManager>(out var injected) && injected.IsAlive())
            {
                return injected;
            }
#endif
            return ScreenManager.HasInstance ? ScreenManager.Instance : null;
        }

        private void SetupGraph()
        {
            this.Log("Setting up ScreenFlow graph from ScreenFlowProfile asset.");
            if (ScreenFlowProfileUtilities.TryGetProfile(out var profile))
            {
                graph = profile.Graph;
                if (!graph)
                {
                    this.LogError("ScreenFlowProfile asset does not contain a valid ScreenFlowGraph.");
                }
            }
            else
            {
                // Without a profile there is no graph to drive: fail bootstrap loudly.
                throw new Exception(
                    $"Could not find ScreenFlowProfile asset at Resources/{ScreenFlowConstants.ProfilePath}. " +
                    "Create it from the ScreenFlow settings tab.");
            }
        }

        public ScreenModel GetStartScreen()
        {
            return _graphResolver.GetStartNode()?.Screen;
        }

        public void Trigger(string eventName)
        {
            _actor.Trigger(eventName);
        }

        public ScreenModel Current => _state.Current;

        public string LastEvent => _state.LastEvent;

        public bool TryGetPrevious(int stepsBack, out ScreenFlowNode node)
        {
            node = null;
            return _state != null && _state.TryGetPrevious(stepsBack, out node);
        }

        public ScreenFlowStateContainer State => _state;

        private void Start()
        {
            // Go into start screen
            _actor.StartFlow();
        }
    }
}
