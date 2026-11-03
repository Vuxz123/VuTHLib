namespace _VuTH.Core.GameCycle.ScreenFlow
{
    /// <summary>
    /// How a <see cref="ScreenFlowTransition"/> drives the ScreenManager. Int values are serialized — do not reorder.
    /// </summary>
    public enum ScreenFlowNavigation
    {
        /// <summary>Start a new flow: close every open screen and enter the target (default).</summary>
        Enter = 0,

        /// <summary>Push the target on top of the current screen (current stays loaded).</summary>
        Push = 1,

        /// <summary>Show the target as an override/interrupt above the stack.</summary>
        PushOverride = 2,

        /// <summary>Pop the current screen. The target node should be the screen below it.
        /// Falls back to Enter when there is nothing to pop.</summary>
        Pop = 3,

        /// <summary>Close the active override. The target node should be the screen under the override.
        /// Falls back to Enter when no override is active.</summary>
        PopOverride = 4,
    }

    /// <summary>For <see cref="ScreenFlowNavigation.Push"/>: whether the screen below is hidden while the target is on top.</summary>
    public enum ScreenBelowVisibility
    {
        /// <summary>Use the target ScreenModel's <c>hideScreenBelow</c> setting.</summary>
        ScreenDefault = 0,
        Hide = 1,
        KeepVisible = 2,
    }
}
