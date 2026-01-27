using Cysharp.Threading.Tasks;

namespace _VuTH.Core.GameCycle.Screen.Loading
{
    public interface ILoadingController
    {
        UniTask Show();
        UniTask Hide();
        void SetProgress(float value);
        void SetVisible(bool isVisible);
    }
}