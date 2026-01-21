using UnityEngine;
using ZLinq;

namespace Core.GameCycle.ScreenFlow.Condition.Logical
{
    [CreateAssetMenu(menuName = "Screen/Screen Flow/Condition/Or")]
    public class OrCondition : TransitionCondition
    {
        [SerializeField] private TransitionCondition[] conditions;
        
        public override bool Evaluate()
        {
            return conditions.AsValueEnumerable()
                .Any(c => c.Evaluate());
        }
    }
}