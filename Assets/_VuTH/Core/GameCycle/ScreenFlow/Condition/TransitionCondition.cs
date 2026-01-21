using UnityEngine;

namespace Core.GameCycle.ScreenFlow.Condition
{
    public abstract class TransitionCondition : ScriptableObject
    {
        public abstract bool Evaluate();
    }
}