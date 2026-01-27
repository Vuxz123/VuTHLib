using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Window.Blocker
{
    /// <summary>
    /// Global input blocker with reference counting
    /// </summary>
    public class UIInputBlocker : MonoBehaviour, IUIInputBlocker
    {
        [SerializeField] private Canvas blockCanvas;
        [SerializeField] private CanvasGroup blockCanvasGroup;
        
        private int _blockCount;
        private readonly HashSet<string> _blockReasons = new();
        
        public bool IsBlocked => _blockCount > 0;
        
        private void Awake()
        {
            if (blockCanvas == null)
            {
                var go = new GameObject("InputBlocker");
                go.transform.SetParent(transform);
                
                blockCanvas = go.AddComponent<Canvas>();
                blockCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                blockCanvas.sortingOrder = 9999;
                
                blockCanvasGroup = go.AddComponent<CanvasGroup>();
                blockCanvasGroup.alpha = 0f;
                blockCanvasGroup.blocksRaycasts = false;
                
                var image = go.AddComponent<UnityEngine.UI.Image>();
                image.color = new Color(0, 0, 0, 0.01f); // Almost invisible but blocks input
            }
            
            blockCanvas.gameObject.SetActive(false);
        }
        
        public void Block(string reason = null)
        {
            _blockCount++;
            
            if (!string.IsNullOrEmpty(reason))
                _blockReasons.Add(reason);
            
            UpdateBlockState();
        }
        
        public void Unblock(string reason = null)
        {
            _blockCount = Mathf.Max(0, _blockCount - 1);
            
            if (!string.IsNullOrEmpty(reason))
                _blockReasons.Remove(reason);
            
            UpdateBlockState();
        }
        
        private void UpdateBlockState()
        {
            var shouldBlock = _blockCount > 0;
            
            blockCanvas.gameObject.SetActive(shouldBlock);
            blockCanvasGroup.blocksRaycasts = shouldBlock;

            Debug.Log(shouldBlock
                ? $"[UIInputBlocker] Blocked (Count: {_blockCount}, Reasons: {string.Join(", ", _blockReasons)})"
                : "[UIInputBlocker] Unblocked");
        }
    }
}