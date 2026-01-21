using UnityEngine;

namespace Core.GameCycle.ScreenFlow.Profile
{
    public class ScreenFlowProfile : ScriptableObject
    {
        [SerializeField] private ScreenFlowGraph graph;
        public ScreenFlowGraph Graph => graph;
    }
}