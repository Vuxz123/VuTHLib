using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Window.Transition
{
    /// <summary>
    /// Scale transition (popup style)
    /// </summary>
    public class ScaleTransition : IUITransition
    {
        public float Duration { get; }
        private readonly AnimationCurve _curve;
        
        public ScaleTransition(float duration = 0.3f, AnimationCurve curve = null)
        {
            Duration = duration;
            _curve = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
        }
        
        public async UniTask In(IUIView view)
        {
            var transform = view.GameObject.transform;
            var cg = view.CanvasGroup;
            
            transform.localScale = Vector3.zero;
            cg.alpha = 0f;
            
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / Duration;
                float curveValue = _curve.Evaluate(t);
                
                transform.localScale = Vector3.one * curveValue;
                cg.alpha = t;
                
                await UniTask.Yield();
            }
            
            transform.localScale = Vector3.one;
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        
        public async UniTask Out(IUIView view)
        {
            var transform = view.GameObject.transform;
            var cg = view.CanvasGroup;
            cg.interactable = false;
            
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / Duration;
                float curveValue = _curve.Evaluate(1f - t);
                
                transform.localScale = Vector3.one * curveValue;
                cg.alpha = 1f - t;
                
                await UniTask.Yield();
            }
            
            transform.localScale = Vector3.zero;
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
        }
    }
}