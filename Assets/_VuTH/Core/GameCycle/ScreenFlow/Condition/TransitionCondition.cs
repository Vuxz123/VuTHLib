using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Condition
{
    public abstract class TransitionCondition : ScriptableObject
    {
        public abstract bool Evaluate();
    }
}