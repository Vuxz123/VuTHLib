using _VuTH.Core.GameCycle.Screen.Loading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Progress
{
    /// <summary>
    /// Smoothly animates the loading bar towards the latest reported value.
    /// <list type="bullet">
    /// <item>Values are clamped to [0, 1] and never go backwards.</item>
    /// <item>Runs on unscaled time, so a paused game (<c>Time.timeScale = 0</c>) cannot freeze the loading screen.</item>
    /// <item><see cref="CompleteAsync"/> drives the bar to 1 itself instead of waiting for tasks to report exactly 1,
    /// so tasks that report fewer/more units than they declared can no longer hang the transition.</item>
    /// </list>
    /// </summary>
    public class ProgressReporter : AbstractProgressReporter
    {
        private readonly ILoadingController _loadingController;
        private readonly float _speed;

        private Tween _loadingTween;
        private float _currentValue;
        private float _targetValue;

        public ProgressReporter(ILoadingController loadingController, float speed)
        {
            _loadingController = loadingController;
            _speed = speed;
        }

        /// <summary>Latest value pushed to the loading controller (for tests / debugging).</summary>
        public float CurrentValue => _currentValue;

        public override void Report(float value)
        {
            value = Mathf.Clamp01(value);
            if (value <= _targetValue) return; // monotonic: ignore stale / backwards reports
            _targetValue = value;

            _loadingTween.Stop();

            var safeSpeed = Mathf.Max(_speed, 0.0001f);
            var duration = Mathf.Abs(value - _currentValue) / safeSpeed;
            if (duration <= 0f)
            {
                SetValue(value);
                return;
            }

            _loadingTween = Tween.Custom(
                startValue: _currentValue,
                endValue: value,
                duration: duration,
                onValueChange: SetValue,
                ease: Ease.OutQuad,
                useUnscaledTime: true);
        }

        public override async UniTask CompleteAsync()
        {
            Report(1f);

            if (_loadingTween.isAlive)
            {
                await UniTask.WaitUntil(this, static state => !state._loadingTween.isAlive);
            }

            // Tween may have been stopped externally (e.g. PrimeTween kill-all); make sure the bar ends full.
            SetValue(1f);
        }

        private void SetValue(float v)
        {
            _currentValue = v;
            _loadingController.SetProgress(v);
        }
    }
}
