using Cysharp.Threading.Tasks;

namespace _VuTH.Core.GameCycle.Screen.Loading.Controllers
{
    internal sealed class NullLoadingController : ILoadingController
    {
        public UniTask Show() => UniTask.CompletedTask;
        public UniTask Hide() => UniTask.CompletedTask;
        public void SetProgress(float value) { }
        public void SetVisible(bool isVisible) { }
    }
}