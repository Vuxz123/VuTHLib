using _VuTH.Core.GameCycle.Screen.Loading;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.GameCycle.Screen.Progress
{
    public class NonLoadingProgressReporter : AbstractProgressReporter
    {
        public NonLoadingProgressReporter(ILoadingController loadingController)
        {
            loadingController.SetVisible(false);
        }

        public override void Report(float value)
        {
            // No-op
        }

        public override UniTask CompleteAsync()
        {
            return UniTask.CompletedTask;
        }
    }
}