using System.Collections.Generic;
using Common.Init;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.GameCycle.Screen.Loading.Tasks
{
    [CreateAssetMenu(menuName = "Screen/Screen Loading Tasks/VInitializable Task", fileName = "VInitializableTask")]
    public class VInitializableTask : ScreenLoadingTask
    {
        private Queue<VInitializeInvokeSite> _siteQueue = new();
        
        public override int AggregateTask(LoadingContext context)
        {
            _siteQueue = new Queue<VInitializeInvokeSite>();
            if (context.MainScene.TryGetVInitializeInvokeSite(out var site))
            {
                _siteQueue.Enqueue(site);
            }

            foreach (var additiveScene in context.AdditiveScenes)
            {
                if (additiveScene.TryGetVInitializeInvokeSite(out var additiveSite))
                {
                    _siteQueue.Enqueue(additiveSite);
                }
            }
            
            return _siteQueue.Count;
        }

        public override async UniTask Execute(LoadingContext context, LoadingHandler handler)
        {
            while (_siteQueue.TryDequeue(out var invokeSite))
            {
                await invokeSite.InvokeInitialize();
                handler.Increment();
            }
        }
    }
}