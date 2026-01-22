using System;
using Common;
using Common.Log;
using Core.GameCycle.Screen;
using Core.GameCycle.ScreenFlow.Profile;
using UnityEngine;

namespace Core.GameCycle.ScreenFlow
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
        private IScreenFlowResolver _resolver;
        
        protected override void InitializeBootstrap()
        {
            SetupGraph();
            _state = new ScreenFlowStateContainer(historyCapacity);
            _resolver = new ScreenFlowGraphResolver(graph);

            // init state
            _state.Reset(_resolver.GetStartNode());

            _actor = new ScreenFlowActor(
                _state,
                _resolver,
                new TransitionContext("screenflow"));
        }

        protected override void DeinitializeBootstrap()
        {
            _actor.Dispose();
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
                this.LogError("Could not find ScreenFlowProfile asset. Creating a new ScreenFlowProfile asset.");
                throw new Exception("Could not find ScreenFlowProfile asset.");
            }
        }

        public ScreenModel GetStartScreen()
        {
            return _resolver.GetStartNode()?.Screen;
        }

        public void Trigger(string eventName)
        {
            _actor.Trigger(eventName);
        }

        public ScreenModel Current => _state.Current;

        public string LastEvent => _state.LastEvent;

        public ScreenFlowStateContainer State => _state;

        private void Start()
        {
            // Go into start screen
            _actor.StartFlow();
        }
    }
}
