using Cysharp.Threading.Tasks;
using PrimeTween;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Fade transition. Runs on unscaled time, so it also plays while the game is paused (<c>Time.timeScale = 0</c>).
    /// Drives the view's dimmer (<see cref="IBackgroundDimmer"/>) along with its alpha.
    /// </summary>
    public class FadeTransition : IUITransition
    {
        public float Duration { get; }

        public FadeTransition() : this(0.3f) { }

        public FadeTransition(float duration = 0.3f)
        {
            Duration = duration;
        }

        public async UniTask In(IUIView view)
        {
            var blocker = view as ITransitionInputBlocker;
            var dimmer = view as IBackgroundDimmer;
            var cg = view.CanvasGroup;

            blocker?.SetInputBlocked(true);
            cg.alpha = 0f;

            var dimTween = dimmer != null
                ? Tween.Custom(dimmer, 0f, 1f, Duration, (d, v) => d.SetDim(v), useUnscaledTime: true)
                : default;
            await Tween.Alpha(cg, 0f, 1f, Duration, useUnscaledTime: true);
            if (dimTween.isAlive) await dimTween;

            dimmer?.SetDim(1f);
            blocker?.SetInputBlocked(false);
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        public async UniTask Out(IUIView view)
        {
            var blocker = view as ITransitionInputBlocker;
            var dimmer = view as IBackgroundDimmer;
            var cg = view.CanvasGroup;

            blocker?.SetInputBlocked(true);
            cg.interactable = false;

            var dimTween = dimmer != null
                ? Tween.Custom(dimmer, 1f, 0f, Duration, (d, v) => d.SetDim(v), useUnscaledTime: true)
                : default;
            await Tween.Alpha(cg, 1f, 0f, Duration, useUnscaledTime: true);
            if (dimTween.isAlive) await dimTween;

            dimmer?.SetDim(0f);
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }
}
