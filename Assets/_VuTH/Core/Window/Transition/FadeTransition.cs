using Cysharp.Threading.Tasks;
using PrimeTween;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Fade transition
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
            var blocker = (view as object) as ITransitionInputBlocker;
            blocker?.SetInputBlocked(true);

            var dimmer = (view as object) as IBackgroundDimmer;

            var cg = view.CanvasGroup;
            cg.alpha = 0f;

            // Drive dimmer with a parallel tween.
            Tween dimTween = default;
            if (dimmer != null)
            {
                dimTween = Tween.Custom(0f, 1f, Duration,
                    onValueChange: v => dimmer.SetDim(v));
            }

            var alphaTween = Tween.Alpha(cg, 0f, 1f, Duration);
            await alphaTween;

            if (dimTween.isAlive)
                await dimTween;

            cg.alpha = 1f;
            dimmer?.SetDim(1f);
            cg.interactable = true;
            cg.blocksRaycasts = true;

            blocker?.SetInputBlocked(false);
        }

        public async UniTask Out(IUIView view)
        {
            var blocker = (view as object) as ITransitionInputBlocker;
            blocker?.SetInputBlocked(true);

            var dimmer = (view as object) as IBackgroundDimmer;

            var cg = view.CanvasGroup;
            cg.interactable = false;

            Tween dimTween = default;
            if (dimmer != null)
            {
                dimTween = Tween.Custom(1f, 0f, Duration,
                    onValueChange: v => dimmer.SetDim(v));
            }

            var alphaTween = Tween.Alpha(cg, 1f, 0f, Duration);
            await alphaTween;

            if (dimTween.isAlive)
                await dimTween;

            cg.alpha = 0f;
            dimmer?.SetDim(0f);
            cg.blocksRaycasts = false;

            blocker?.SetInputBlocked(false);
        }
    }
}