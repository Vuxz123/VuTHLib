using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Condition.Logical
{
    /// <summary>
    /// Negates its child. A missing (null) child is a configuration error: it is logged and the result is false,
    /// so a broken Not never opens a transition by accident.
    /// </summary>
    [CreateAssetMenu(menuName = "Screen/Screen Flow/Condition/Not")]
    public class NotCondition : TransitionCondition
    {
        [SerializeField] private TransitionCondition conditions;

        public override bool Evaluate()
        {
            if (!conditions)
            {
                Debug.LogError($"[{nameof(NotCondition)}] '{name}' has no child condition assigned.", this);
                return false;
            }

            return !conditions.Evaluate();
        }
    }
}
