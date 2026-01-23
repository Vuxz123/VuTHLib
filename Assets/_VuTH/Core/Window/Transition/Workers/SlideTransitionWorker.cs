namespace Core.Window.Transition.Workers
{
    public sealed class SlideTransitionWorker : ComplexPresetTransitionWorkerBase
    {
        protected override bool CanHandleInternal(string presetName)
        {
            return StartsWith(presetName, "Slide");
        }

        protected override IUITransition CreateInternal(string presetName)
        {
            // Supported:
            // - Slide
            // - Slide:Left|Right|Top|Bottom
            var dir = SlideTransition.Direction.Right;

            var parts = presetName.Split(':');
            if (parts.Length >= 2)
            {
                var token = parts[1].Trim();
                if (System.Enum.TryParse(token, ignoreCase: true, out SlideTransition.Direction parsed))
                    dir = parsed;
            }

            return new SlideTransition(dir);
        }
    }
}
