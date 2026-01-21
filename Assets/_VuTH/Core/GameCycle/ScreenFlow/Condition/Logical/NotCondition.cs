using UnityEngine;
using UnityEngine.Serialization;

namespace Core.GameCycle.ScreenFlow.Condition.Logical
{
    [CreateAssetMenu(menuName = "Screen/Screen Flow/Condition/Not")]
    public class NotCondition : TransitionCondition
    {
        [SerializeField] private TransitionCondition conditions;
        
        public override bool Evaluate()
        {
            return !conditions.Evaluate();
        }
    }
}