using Cysharp.Threading.Tasks;

namespace Core.GameCycle.Screen.Loading
{
    internal sealed class NullLoadingController : ILoadingController
    {
        public UniTask Show() => UniTask.CompletedTask;
        public UniTask Hide() => UniTask.CompletedTask;
        public void SetProgress(float value) { }
        public void SetVisible(bool isVisible) { }
    }
}