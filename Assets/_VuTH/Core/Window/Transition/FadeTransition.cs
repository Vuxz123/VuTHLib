using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Window.Transition
{
    /// <summary>
    /// Fade transition
    /// </summary>
    public class FadeTransition : IUITransition
    {
        public float Duration { get; }
        
        public FadeTransition(float duration = 0.3f)
        {
            Duration = duration;
        }
        
        public async UniTask In(IUIView view)
        {
            var cg = view.CanvasGroup;
            cg.alpha = 0f;
            
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(0f, 1f, elapsed / Duration);
                await UniTask.Yield();
            }
            
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        
        public async UniTask Out(IUIView view)
        {
            var cg = view.CanvasGroup;
            cg.interactable = false;
            
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(1f, 0f, elapsed / Duration);
                await UniTask.Yield();
            }
            
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
        }
    }
}