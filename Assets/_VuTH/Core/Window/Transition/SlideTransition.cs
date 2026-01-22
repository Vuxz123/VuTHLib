using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Window.Transition
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
        
        public SlideTransition(Direction direction, float duration = 0.3f, float distance = 1000f)
        {
            _direction = direction;
            Duration = duration;
            _distance = distance;
        }
        
        public async UniTask In(IUIView view)
        {
            var rt = view.GameObject.GetComponent<RectTransform>();
            var cg = view.CanvasGroup;
            
            Vector2 startPos = GetOffscreenPosition(rt);
            Vector2 endPos = rt.anchoredPosition;
            
            rt.anchoredPosition = startPos;
            cg.alpha = 1f;
            
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / Duration;
                
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                await UniTask.Yield();
            }
            
            rt.anchoredPosition = endPos;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        
        public async UniTask Out(IUIView view)
        {
            var rt = view.GameObject.GetComponent<RectTransform>();
            var cg = view.CanvasGroup;
            cg.interactable = false;
            
            Vector2 startPos = rt.anchoredPosition;
            Vector2 endPos = GetOffscreenPosition(rt);
            
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / Duration;
                
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                await UniTask.Yield();
            }
            
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