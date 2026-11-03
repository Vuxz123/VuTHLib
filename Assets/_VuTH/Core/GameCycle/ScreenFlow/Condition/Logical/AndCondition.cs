using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Condition.Logical
{
    /// <summary>
    /// True when every child is true. Empty list = true (vacuous truth).
    /// A missing (null) child is a configuration error: it is logged and counts as false.
    /// </summary>
    [CreateAssetMenu(menuName = "Screen/Screen Flow/Condition/And")]
    public class AndCondition : TransitionCondition
    {
        [SerializeField] private List<TransitionCondition> conditions;

        public override bool Evaluate()
        {
            if (conditions == null) return true;

            foreach (var c in conditions)
            {
                if (!LogicalConditionUtility.EvaluateChild(this, c)) return false;
            }

            return true;
        }
    }
}
