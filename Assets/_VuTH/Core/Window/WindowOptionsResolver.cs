namespace _VuTH.Core.Window
{
    /// <summary>
    /// Merges caller-supplied <see cref="WindowOptions"/> with the per-window defaults declared by an
    /// <see cref="IWindowDefinition"/>. Pure C# so the merge rules can be unit tested.
    /// </summary>
    public static class WindowOptionsResolver
    {
        /// <summary>
        /// Heuristic merge: fields the caller left at their "common default" values (or null) are overridden
        /// from <paramref name="definition"/>; explicitly customized fields keep the caller's value.
        /// Mutates <paramref name="options"/> in place. No-op when either argument is null.
        /// </summary>
        public static void ApplyDefaults(WindowOptions options, IWindowDefinition definition)
        {
            if (definition == null) return;
            if (options == null) return;

            // Heuristic merge: if caller left fields at the "common default" values, override from definition.
            // If caller explicitly customized, keep caller values.
            if (options.WindowType == WindowType.Popup)
                options.WindowType = definition.WindowType;

            // Data-driven settings (preferred)
            options.TransitionInSettings ??= definition.TransitionInSettings;
            options.TransitionOutSettings ??= definition.TransitionOutSettings;

            // Legacy preset fallback (single preset for both directions)
            if ((options.TransitionInSettings == null && options.TransitionOutSettings == null)
                && (string.IsNullOrEmpty(options.TransitionPreset) || options.TransitionPreset == "Scale"))
            {
                options.TransitionPreset = definition.TransitionPreset;
            }

            // Nullable bools: only default from definition when caller didn't specify.
            options.CloseOnBackPress ??= definition.CloseOnBackPress;
            options.BlockInput ??= definition.BlockInput;
        }
    }
}
