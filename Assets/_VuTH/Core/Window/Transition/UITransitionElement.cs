using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Animates one element of a window (title, button, item) after the window's transition in, with its own delay,
    /// e.g. buttons popping in one after another. The element is hidden while the window opens, then played; the window
    /// manager does it for every active element of the window on each opening. Unscaled time.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UITransitionElement : MonoBehaviour
    {
        public enum Effect
        {
            Fade,
            Scale,
            SlideUp,
            SlideDown,
            SlideLeft,
            SlideRight,
        }

        [SerializeField] private Effect effect = Effect.Scale;
        [SerializeField, Min(0f)] private float delay;
        [SerializeField, Min(0f)] private float duration = 0.25f;

        [Tooltip("Slide distance in anchored units.")]
        [SerializeField] private float distance = 60f;

        private CanvasGroup _group;
        private RectTransform _rect;
        private bool _restCaptured;
        private Vector3 _restScale;
        private Vector2 _restPosition;

        public float Delay => delay;
        public float Duration => duration;

        private void EnsureRest()
        {
            if (_restCaptured) return;
            _restCaptured = true;

            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (!_group) _group = gameObject.AddComponent<CanvasGroup>();
            _restScale = _rect.localScale;
            _restPosition = _rect.anchoredPosition;
        }

        /// <summary>Puts the element in its start state (hidden / off its rest position).</summary>
        public void PrepareHidden()
        {
            EnsureRest();
            Tween.StopAll(_group);
            Tween.StopAll(_rect);

            _group.alpha = 0f;
            _rect.localScale = effect == Effect.Scale ? Vector3.zero : _restScale;
            _rect.anchoredPosition = _restPosition + SlideOffset();
        }

        /// <summary>Plays the element from its start state to its rest state.</summary>
        public async UniTask PlayIn()
        {
            EnsureRest();

            var fade = Tween.Alpha(_group, 0f, 1f, duration, Ease.OutQuad, startDelay: delay, useUnscaledTime: true);
            var move = effect switch
            {
                Effect.Fade => default,
                Effect.Scale => Tween.Scale(_rect, Vector3.zero, _restScale, duration, Ease.OutBack,
                    startDelay: delay, useUnscaledTime: true),
                _ => Tween.UIAnchoredPosition(_rect, _restPosition + SlideOffset(), _restPosition, duration,
                    Ease.OutCubic, startDelay: delay, useUnscaledTime: true),
            };

            await fade;
            if (move.isAlive) await move;
            ShowAtRest();
        }

        /// <summary>Rest state: visible, in place.</summary>
        public void ShowAtRest()
        {
            EnsureRest();
            _group.alpha = 1f;
            _rect.localScale = _restScale;
            _rect.anchoredPosition = _restPosition;
        }

        private Vector2 SlideOffset() => effect switch
        {
            Effect.SlideUp => new Vector2(0f, -distance),
            Effect.SlideDown => new Vector2(0f, distance),
            Effect.SlideLeft => new Vector2(distance, 0f),
            Effect.SlideRight => new Vector2(-distance, 0f),
            _ => Vector2.zero,
        };

        #region Window helpers

        private static readonly List<UITransitionElement> Buffer = new();

        /// <summary>Hides every active element of <paramref name="view"/> before its transition in.</summary>
        internal static void PrepareAll(IUIView view)
        {
            view.GameObject.GetComponentsInChildren(false, Buffer);
            foreach (var element in Buffer) element.PrepareHidden();
            Buffer.Clear();
        }

        /// <summary>Stops the tweens of every element of <paramref name="view"/> (window removed mid-transition).</summary>
        internal static void StopAll(IUIView view)
        {
            view.GameObject.GetComponentsInChildren(true, Buffer);
            foreach (var element in Buffer)
            {
                if (!element._restCaptured) continue;
                Tween.StopAll(element._group);
                Tween.StopAll(element._rect);
            }

            Buffer.Clear();
        }

        /// <summary>Plays every active element of <paramref name="view"/> (each with its delay) and waits for all.</summary>
        internal static UniTask PlayAll(IUIView view)
        {
            view.GameObject.GetComponentsInChildren(false, Buffer);
            if (Buffer.Count == 0) return UniTask.CompletedTask;

            var playing = new List<UniTask>(Buffer.Count);
            foreach (var element in Buffer) playing.Add(element.PlayIn());
            Buffer.Clear();
            return UniTask.WhenAll(playing);
        }

        #endregion
    }
}
