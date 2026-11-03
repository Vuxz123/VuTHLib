using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Condition.Logical
{
    /// <summary>
    /// True when at least one child is true. Empty list = false.
    /// A missing (null) child is a configuration error: it is logged and counts as false.
    /// </summary>
    [CreateAssetMenu(menuName = "Screen/Screen Flow/Condition/Or")]
    public class OrCondition : TransitionCondition
    {
        [SerializeField] private TransitionCondition[] conditions;

        public override bool Evaluate()
        {
            if (conditions == null) return false;

            foreach (var c in conditions)
            {
                if (LogicalConditionUtility.EvaluateChild(this, c)) return true;
            }

            return false;
        }
    }
}
