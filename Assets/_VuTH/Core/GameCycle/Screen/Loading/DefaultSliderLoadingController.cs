using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace _VuTH.Core.GameCycle.Screen.Loading
{
    /// <summary>
    /// Simple loading UI driven by a Slider.
    /// Assign this component to ScreenManager.loadingController.
    /// </summary>
    public sealed class DefaultSliderLoadingController : MonoBehaviour, ILoadingController
    {
        [Header("References")]
        [Tooltip("Root GameObject of the loading UI. If null, uses this.gameObject.")]
        [SerializeField]
        private GameObject root;

        [Tooltip("Optional. If assigned, we'll also control alpha/interactable/raycast.")] [SerializeField]
        private CanvasGroup canvasGroup;

        [Tooltip("Slider used to display progress (0..1).")] [SerializeField]
        private Slider progressSlider;

        [Header("Animation")] [Tooltip("Duration of show/hide animations in seconds.")] [SerializeField]
        private float animationDuration = 0.25f;

        [Header("Behavior")] [Tooltip("If true, Show() resets progress to 0.")] [SerializeField]
        private bool resetProgressOnShow = true;

        [Tooltip("If true, Hide() forces progress to 1.")] [SerializeField]
        private bool completeProgressOnHide = false;

        private Tween _currentTween;

        private void Awake()
        {
            if (!root) root = gameObject;

            // Start hidden by default to avoid flashing a loading overlay on boot.
            SetVisible(false);

            // Ensure slider starts at 0.
            if (progressSlider) progressSlider.value = 0f;
        }

        public async UniTask Show()
        {
            if (resetProgressOnShow) SetProgress(0f);
            await FadeIn();
        }

        public async UniTask Hide()
        {
            if (completeProgressOnHide) SetProgress(1f);
            await FadeOut();
        }

        public void SetProgress(float value)
        {
            if (!progressSlider) return;
            progressSlider.value = Mathf.Clamp01(value);
        }

        private async UniTask FadeIn()
        {
            if (_currentTween.isAlive) _currentTween.Stop();
            SetVisible(true);
            if (!canvasGroup)
            {
                return;
            }

            canvasGroup.gameObject.SetActive(true);

            _currentTween = Tween.Alpha(canvasGroup, 0, 1, animationDuration, ease: Ease.OutCirc, useUnscaledTime: true);
            await _currentTween;
        }

        private async UniTask FadeOut()
        {
            if (_currentTween.isAlive) _currentTween.Stop();
            if (!canvasGroup)
            {
                SetVisible(false);
                return;
            }

            canvasGroup.gameObject.SetActive(true);

            _currentTween = Tween.Alpha(canvasGroup, canvasGroup.alpha, 0, animationDuration, ease: Ease.InCirc, useUnscaledTime: true)
                .OnComplete(() => SetVisible(false));
            await _currentTween;
        }

        public void SetVisible(bool visible)
        {
            if (root) root.SetActive(visible);

            if (!canvasGroup) return;
            if (visible) canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void OnDestroy()
        {
            if (_currentTween.isAlive) _currentTween.Stop();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!root) root = gameObject;
        }
#endif
    }
}
