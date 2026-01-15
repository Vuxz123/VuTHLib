using System.Collections.Generic;
using UnityEngine;

namespace Core.GameCycle.ScreenFlow
{
    [CreateAssetMenu(
        fileName = "ScreenFlowGraph",
        menuName = "Screen/Screen Flow/Screen Flow Graph"
    )]
    public class ScreenFlowGraph : ScriptableObject
    {
        public const string FileExtension = ".sf";
        
        [Header("Graph")]
        [SerializeField] private string startNodeGuid;

        [SerializeField] private List<ScreenFlowNode> nodes = new();
        [SerializeField] private List<ScreenFlowTransition> transitions = new();

        public string StartNodeGuid => startNodeGuid;
        public IReadOnlyList<ScreenFlowNode> Nodes => nodes;
        public IReadOnlyList<ScreenFlowTransition> Transitions => transitions;

#if UNITY_EDITOR
        public void SetStartNode(string guid)
        {
            startNodeGuid = guid;
        }
#endif
    }
}