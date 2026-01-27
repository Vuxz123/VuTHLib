namespace _VuTH.Core.Window.Transition.Workers
{
    public sealed class FadeTransitionWorker : SimplePresetTransitionWorkerBase
    {
        protected override string Preset => "Fade";

        protected override IUITransition CreateInternal()
        {
            return new FadeTransition();
        }
    }
}
