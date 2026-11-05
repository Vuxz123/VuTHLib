using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Scale transition (popup style): scales the transition root from zero to its rest scale while fading in.
    /// Runs on unscaled time, so it also plays while the game is paused.
    /// </summary>
    public class ScaleTransition : IUITransition
    {
        public float Duration { get; }

        // Null: the default OutBack / InBack eases.
        private readonly AnimationCurve _curve;

        public ScaleTransition() : this(0.3f) { }

        /// <param name="duration">Seconds.</param>
        /// <param name="curve">Scale curve (0..1) used both ways; null or empty keeps the default eases.</param>
        public ScaleTransition(float duration = 0.3f, AnimationCurve curve = null)
        {
            Duration = duration;
            _curve = curve != null && curve.length >= 2 ? curve : null;
        }

        private Easing InEase => _curve != null ? Easing.Curve(_curve) : Ease.OutBack;
        private Easing OutEase => _curve != null ? Easing.Curve(_curve) : Ease.InBack;

        public async UniTask In(IUIView view)
        {
            var rt = TransitionUtils.GetTransitionRoot(view);
            var cg = view.CanvasGroup;
            cg.alpha = 0f;

            Tween scaleTween = default;
            var restScale = Vector3.one;
            if (rt != null)
            {
                restScale = rt.localScale;
                rt.localScale = Vector3.zero;
                scaleTween = Tween.Scale(rt, Vector3.zero, restScale, Duration, InEase, useUnscaledTime: true);
            }

            await Tween.Alpha(cg, 0f, 1f, Duration, Ease.OutQuad, useUnscaledTime: true);
            if (scaleTween.isAlive) await scaleTween;

            if (rt != null) rt.localScale = restScale;
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        public async UniTask Out(IUIView view)
        {
            var rt = TransitionUtils.GetTransitionRoot(view);
            var cg = view.CanvasGroup;
            cg.interactable = false;

            var scaleTween = rt != null
                ? Tween.Scale(rt, rt.localScale, Vector3.zero, Duration, OutEase, useUnscaledTime: true)
                : default;

            await Tween.Alpha(cg, 1f, 0f, Duration, Ease.InQuad, useUnscaledTime: true);
            if (scaleTween.isAlive) await scaleTween;

            // The rest scale is restored before the next opening.
            if (rt != null) rt.localScale = Vector3.zero;
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
        }
    }
}
