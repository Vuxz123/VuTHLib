using System;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace Core.GameCycle.Screen.Loading.Tasks
{
    [CreateAssetMenu(menuName = "VuTH Core/GameCycle/Screen Loading Tasks/Fake Delay Task", fileName = "FakeDelayTask")]
    public class FakeDelayTask : ScreenLoadingTask
    {
        [Min(0f)] public float seconds = 2f;

        public override int AggregateTask(LoadingContext context)
        {
            return 1;
        }

        public override async UniTask Execute(LoadingContext context, LoadingHandler handler)
        {
            if (seconds <= 0f)
            {
                handler.Increment();
                return;
            }

            await Tween.Delay(seconds, handler.Increment);
        }
    }
}

