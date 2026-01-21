using UnityEngine;

namespace Core.GameCycle.ScreenFlow.Condition
{
    [CreateAssetMenu(menuName = "Screen/Screen Flow/Condition/Always True")]
    public class AlwaysTrueCondition : TransitionCondition
    {
        public override bool Evaluate() => true;
    }
}