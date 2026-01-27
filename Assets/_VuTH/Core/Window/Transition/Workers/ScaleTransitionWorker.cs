namespace _VuTH.Core.Window.Transition.Workers
{
    public sealed class ScaleTransitionWorker : SimplePresetTransitionWorkerBase
    {
        protected override string Preset => "Scale";

        protected override IUITransition CreateInternal()
        {
            return new ScaleTransition();
        }
    }
}
