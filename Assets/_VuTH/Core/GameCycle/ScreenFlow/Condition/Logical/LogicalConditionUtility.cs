using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Condition.Logical
{
    internal static class LogicalConditionUtility
    {
        /// <summary>Evaluates a child of a composite condition; a null child is logged and counts as false.</summary>
        public static bool EvaluateChild(TransitionCondition owner, TransitionCondition child)
        {
            if (!child)
            {
                Debug.LogError($"[{owner.GetType().Name}] '{owner.name}' has an empty (null) child condition.", owner);
                return false;
            }

            return child.Evaluate();
        }
    }
}
