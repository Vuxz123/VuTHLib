using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Loading.Tasks
{
    /// <summary>
    /// Defines a piece of async logic to be executed during screen transition loading.
    /// Implement as ScriptableObject for easy authoring/config.
    /// </summary>
    public abstract class ScreenLoadingTask : ScriptableObject
    {
        [TextArea] public string description;

        public abstract int AggregateTask(LoadingContext context);

        /// <summary>
        /// Execute this task.
        /// </summary>
        /// <param name="context"> The loading context for this transition.</param>
        /// <param name="reporter"> Reports progress (0..1) for this task.</param>
        public abstract UniTask Execute(LoadingContext context, [NotNull] LoadingHandler reporter);
    }
}

