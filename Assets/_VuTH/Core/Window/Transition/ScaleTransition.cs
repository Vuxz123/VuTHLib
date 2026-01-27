using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Scale transition (popup style)
    /// </summary>
    public class ScaleTransition : IUITransition
    {
        public float Duration { get; }
        private readonly AnimationCurve _curve;

        public ScaleTransition() : this(0.3f) { }

        public ScaleTransition(float duration = 0.3f, AnimationCurve curve = null)
        {
            Duration = duration;
            _curve = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
        }

        private Ease GetEaseFallback()
        {
            // PrimeTween uses Ease enums. We keep AnimationCurve field for backward compatibility,
            // but map to a reasonable default ease.
            // If you want exact curve mapping, we can implement Tween.Custom based on _curve.Evaluate(t).
            return Ease.OutBack;
        }

        public async UniTask In(IUIView view)
        {
            var rt = TransitionUtils.GetTransitionRoot(view);
            var cg = view.CanvasGroup;

            cg.alpha = 0f;

            Tween scaleTween = default;
            if (rt != null)
            {
                rt.localScale = Vector3.zero;
                scaleTween = Tween.Scale(rt, Vector3.zero, Vector3.one, Duration, ease: GetEaseFallback());
            }

            var alphaTween = Tween.Alpha(cg, 0f, 1f, Duration, ease: Ease.OutQuad);

            await alphaTween;
            if (scaleTween.isAlive)
                await scaleTween;

            if (rt != null)
                rt.localScale = Vector3.one;

            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        public async UniTask Out(IUIView view)
        {
            var rt = TransitionUtils.GetTransitionRoot(view);
            var cg = view.CanvasGroup;
            cg.interactable = false;

            Tween scaleTween = default;
            if (rt != null)
            {
                scaleTween = Tween.Scale(rt, Vector3.one, Vector3.zero, Duration, ease: Ease.InBack);
            }

            var alphaTween = Tween.Alpha(cg, 1f, 0f, Duration, ease: Ease.InQuad);

            await alphaTween;
            if (scaleTween.isAlive)
                await scaleTween;

            if (rt != null)
                rt.localScale = Vector3.zero;

            cg.alpha = 0f;
            cg.blocksRaycasts = false;
        }
    }
}