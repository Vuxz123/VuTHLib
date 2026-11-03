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
        [SerializeField] private ScreenFlowNavigation navigation = ScreenFlowNavigation.Enter;
        [SerializeField] private ScreenBelowVisibility screenBelow = ScreenBelowVisibility.ScreenDefault;

        public string Guid => guid;
        public string FromNodeGuid => fromNodeGuid;
        public string ToNodeGuid => toNodeGuid;
        public string EventName => eventName;
        public TransitionCondition Condition => condition;
        public ScreenFlowNavigation Navigation => navigation;
        public ScreenBelowVisibility ScreenBelow => screenBelow;

#if UNITY_EDITOR
        public ScreenFlowTransition(
            string from,
            string to,
            string eventName,
            TransitionCondition condition,
            ScreenFlowNavigation navigation = ScreenFlowNavigation.Enter,
            ScreenBelowVisibility screenBelow = ScreenBelowVisibility.ScreenDefault)
        {
            guid = System.Guid.NewGuid().ToString();
            fromNodeGuid = from;
            toNodeGuid = to;
            this.eventName = eventName;
            this.condition = condition;
            this.navigation = navigation;
            this.screenBelow = screenBelow;
        }
#endif

        public override string ToString()
        {
            return navigation == ScreenFlowNavigation.Enter
                ? $"{fromNodeGuid} --[{eventName}]--> {toNodeGuid}"
                : $"{fromNodeGuid} --[{eventName} / {navigation}]--> {toNodeGuid}";
        }
    }
}