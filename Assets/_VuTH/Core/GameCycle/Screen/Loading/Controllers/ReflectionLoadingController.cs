using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Loading.Controllers
{
    internal sealed class ReflectionLoadingController : ILoadingController
    {
        private readonly MonoBehaviour _target;

        public ReflectionLoadingController(MonoBehaviour target)
        {
            _target = target;
        }

        public UniTask Show() => InvokeUniTask(nameof(Show));
        
        public UniTask Hide() => InvokeUniTask(nameof(Hide));
        
        public void SetProgress(float value) => InvokeVoid(nameof(SetProgress), value);
        
        public void SetVisible(bool isVisible)
        {
            
        }

        private UniTask InvokeUniTask(string method)
        {
            if (_target == null) return UniTask.CompletedTask;
            var mi = _target.GetType().GetMethod(method);
            if (mi == null) return UniTask.CompletedTask;
            var result = mi.Invoke(_target, null);
            return result is UniTask ut ? ut : UniTask.CompletedTask;
        }

        private void InvokeVoid(string method, float value)
        {
            if (_target == null) return;
            var mi = _target.GetType().GetMethod(method);
            mi?.Invoke(_target, new object[] { value });
        }
    }
}