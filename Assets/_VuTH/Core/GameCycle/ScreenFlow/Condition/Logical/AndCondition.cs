using System.Collections.Generic;
using UnityEngine;

namespace Core.GameCycle.ScreenFlow.Condition.Logical
{
    [CreateAssetMenu(menuName = "Screen/Screen Flow/Condition/And")]
    public class AndCondition : TransitionCondition
    {
        [SerializeField] private List<TransitionCondition> conditions;

        public override bool Evaluate()
            => conditions.TrueForAll(c => c.Evaluate());
    }
}