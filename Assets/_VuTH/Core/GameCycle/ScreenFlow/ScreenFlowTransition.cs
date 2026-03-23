using System;
using _VuTH.Core.GameCycle.ScreenFlow.Condition;
using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow
{
    [Serializable]
    public class ScreenFlowTransition
    {
        [SerializeField] private string guid;
        [SerializeField] private string fromNodeGuid;
        [SerializeField] private string toNodeGuid;

        [SerializeField] private string eventName;

        [SerializeField] private TransitionCondition condition;

        public string Guid => guid;
        public string FromNodeGuid => fromNodeGuid;
        public string ToNodeGuid => toNodeGuid;
        public string EventName => eventName;
        public TransitionCondition Condition => condition;

#if UNITY_EDITOR
        public ScreenFlowTransition(
            string from,
            string to,
            string eventName,
            TransitionCondition condition)
        {
            guid = System.Guid.NewGuid().ToString();
            fromNodeGuid = from;
            toNodeGuid = to;
            this.eventName = eventName;
            this.condition = condition;
        }
#endif

        public override string ToString()
        {
            return $"{fromNodeGuid} --[{eventName}]--> {toNodeGuid}";
        }
    }
}