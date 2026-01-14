using Core.GameCycle.Screen.Loading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace Core.GameCycle.Screen.Progress
{
    public class ProgressReporter : AbstractProgressReporter
    {
        private readonly ILoadingController _loadingController;
        private readonly float _speed;
        
        private Tween _loadingTween;
        private float _currentValue;

        public ProgressReporter(ILoadingController loadingController, float speed)
        {
            _loadingController = loadingController;
            _speed = speed;
        }

        public override void Report(float value)
        {
            // Stop previous tween if any.
            _loadingTween.Stop();

            // Avoid division by zero / negative speed causing NaN/Infinity durations.
            var safeSpeed = Mathf.Max(_speed, 0.0001f);
            var duration = Mathf.Max(0f, Mathf.Abs(value - _currentValue) / safeSpeed);

            _loadingTween = Tween.Custom(
                startValue: _currentValue,
                endValue: value,
                duration: duration,
                onValueChange: v =>
                {
                    _currentValue = v;
                    _loadingController.SetProgress(_currentValue);
                },
                ease: Ease.OutQuad);
        }
        
        public override async UniTask CompleteAsync()
        {
            // Avoid closure allocation by using the stateful overload.
            await UniTask.WaitUntil(this, static state => 
                Mathf.Approximately(state._currentValue, 1f));
        }
    }
}