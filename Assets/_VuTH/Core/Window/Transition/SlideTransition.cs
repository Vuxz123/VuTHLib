using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Slide transition (from side)
    /// </summary>
    public class SlideTransition : IUITransition
    {
        public enum Direction { Left, Right, Top, Bottom }

        public float Duration { get; }
        private readonly Direction _direction;
        private readonly float _distance;

        public SlideTransition() : this(Direction.Right) { }

        public SlideTransition(Direction direction, float duration = 0.3f, float distance = 1000f)
        {
            _direction = direction;
            Duration = duration;
            _distance = distance;
        }

        public async UniTask In(IUIView view)
        {
            var rt = TransitionUtils.GetTransitionRoot(view);
            var cg = view.CanvasGroup;

            if (rt == null)
            {
                // Fallback: no rect transform to animate
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                return;
            }

            var startPos = GetOffscreenPosition(rt);
            var endPos = rt.anchoredPosition;

            rt.anchoredPosition = startPos;
            cg.alpha = 1f;

            var t = Tween.UIAnchoredPosition(rt, startPos, endPos, Duration, ease: Ease.OutCubic);
            await t;

            rt.anchoredPosition = endPos;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        public async UniTask Out(IUIView view)
        {
            var rt = TransitionUtils.GetTransitionRoot(view);
            var cg = view.CanvasGroup;
            cg.interactable = false;

            if (rt == null)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                return;
            }

            var startPos = rt.anchoredPosition;
            var endPos = GetOffscreenPosition(rt);

            var t = Tween.UIAnchoredPosition(rt, startPos, endPos, Duration, ease: Ease.InCubic);
            await t;

            rt.anchoredPosition = endPos;
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
        }

        private Vector2 GetOffscreenPosition(RectTransform rt)
        {
            return _direction switch
            {
                Direction.Left => new Vector2(-_distance, rt.anchoredPosition.y),
                Direction.Right => new Vector2(_distance, rt.anchoredPosition.y),
                Direction.Top => new Vector2(rt.anchoredPosition.x, _distance),
                Direction.Bottom => new Vector2(rt.anchoredPosition.x, -_distance),
                _ => rt.anchoredPosition
            };
        }
    }
}