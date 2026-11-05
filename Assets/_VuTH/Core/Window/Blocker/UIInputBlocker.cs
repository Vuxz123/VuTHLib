using System.Collections.Generic;
using _VuTH.Common.Log;
using UnityEngine;
using UnityEngine.UI;

namespace _VuTH.Core.Window.Blocker
{
    /// <summary>
    /// Global input blocker with reference counting: a full-screen invisible overlay that swallows clicks while at least
    /// one <see cref="Block"/> has no matching <see cref="Unblock"/>.
    /// </summary>
    public class UIInputBlocker : MonoBehaviour, IUIInputBlocker
    {
        [SerializeField] private Canvas blockCanvas;
        [SerializeField] private CanvasGroup blockCanvasGroup;

        private int _blockCount;
        private readonly Dictionary<string, int> _reasons = new();

        public bool IsBlocked => _blockCount > 0;

        /// <summary>Reasons currently blocking, for debugging.</summary>
        public IReadOnlyCollection<string> Reasons => _reasons.Keys;

        private void Awake()
        {
            EnsureOverlay();
            ApplyState();
        }

        private void EnsureOverlay()
        {
            if (!blockCanvas)
            {
                var go = new GameObject("InputBlocker", typeof(RectTransform));
                go.transform.SetParent(transform, false);

                blockCanvas = go.AddComponent<Canvas>();
                blockCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                blockCanvas.sortingOrder = 9999;
                go.AddComponent<GraphicRaycaster>(); // without it the overlay does not block anything

                var image = go.AddComponent<Image>();
                image.color = new Color(0, 0, 0, 0); // invisible, still a raycast target
                image.raycastTarget = true;
            }

            if (!blockCanvasGroup)
            {
                blockCanvasGroup = blockCanvas.GetComponent<CanvasGroup>();
                if (!blockCanvasGroup) blockCanvasGroup = blockCanvas.gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void Block(string reason = null)
        {
            _blockCount++;

            if (!string.IsNullOrEmpty(reason))
                _reasons[reason] = _reasons.TryGetValue(reason, out var count) ? count + 1 : 1;

            ApplyState();
        }

        public void Unblock(string reason = null)
        {
            if (_blockCount == 0)
            {
                this.LogWarning($"Unblock('{reason}') without a matching Block.");
                return;
            }

            _blockCount--;

            if (!string.IsNullOrEmpty(reason) && _reasons.TryGetValue(reason, out var count))
            {
                if (count <= 1) _reasons.Remove(reason);
                else _reasons[reason] = count - 1;
            }

            ApplyState();
        }

        private void ApplyState()
        {
            if (!blockCanvas) EnsureOverlay();

            var shouldBlock = _blockCount > 0;
            blockCanvas.gameObject.SetActive(shouldBlock);
            blockCanvasGroup.blocksRaycasts = shouldBlock;
        }
    }
}
