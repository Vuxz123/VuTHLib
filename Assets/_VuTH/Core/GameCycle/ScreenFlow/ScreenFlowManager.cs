using System;
using Common.SharedLib;
using Core.GameCycle.Screen;
using UnityEngine;

namespace Core.GameCycle.ScreenFlow
{
    /// <summary>
    /// Public facade: wraps StateContainer + Actor. External callers should only depend on IScreenFlowManager.
    /// </summary>
    public sealed class ScreenFlowManager : VBoostrapManager<ScreenFlowManager, IScreenFlowManager> , IScreenFlowManager
    {
        [Header("Screen Flow")]
        [SerializeField] private ScreenFlowGraph graph;
        
        [Header("Settings")]
        [SerializeField] private int historyCapacity = 32;
        
        private ScreenFlowStateContainer _state;
        private ScreenFlowActor _actor;
        private IScreenFlowResolver _resolver;
        
        protected override void InitializeBootstrap()
        {
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
    }
}
