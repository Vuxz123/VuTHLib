namespace _VuTH.Core.Window
{
    /// <summary>
    /// Merges caller-supplied <see cref="WindowOptions"/> with the per-window defaults declared by an
    /// <see cref="IWindowDefinition"/>. Pure C# so the merge rules can be unit tested.
    /// </summary>
    public static class WindowOptionsResolver
    {
        /// <summary>
        /// Fills every field the caller left unset (null / empty) from <paramref name="definition"/>; fields the caller
        /// set are kept, whatever their value. Mutates <paramref name="options"/>. No-op when either argument is null.
        /// </summary>
        public static void ApplyDefaults(WindowOptions options, IWindowDefinition definition)
        {
            if (definition == null) return;
            if (options == null) return;

            options.WindowType ??= definition.WindowType;
            options.TransitionInSettings ??= definition.TransitionInSettings;
            options.TransitionOutSettings ??= definition.TransitionOutSettings;

            if (string.IsNullOrEmpty(options.TransitionPreset))
                options.TransitionPreset = definition.TransitionPreset;

            options.CloseOnBackPress ??= definition.CloseOnBackPress;
            options.BlockInput ??= definition.BlockInput;
            options.Duplicate ??= definition.DuplicatePolicy;
            options.KeepOnScreenChange ??= definition.KeepOnScreenChange;
            options.KeepInstance ??= definition.KeepInstance;
        }

        /// <summary>
        /// A copy of <paramref name="options"/> with every field set: caller values, then <paramref name="definition"/>,
        /// then the built-in defaults. <paramref name="options"/> itself is not changed.
        /// </summary>
        public static WindowOptions Resolve(WindowOptions options, IWindowDefinition definition)
        {
            var resolved = options?.Clone() ?? new WindowOptions();
            ApplyDefaults(resolved, definition);

            resolved.WindowType ??= WindowType.Popup;
            if (string.IsNullOrEmpty(resolved.TransitionPreset))
                resolved.TransitionPreset = WindowOptions.DefaultTransitionPreset;
            resolved.CloseOnBackPress ??= true;
            resolved.BlockInput ??= true;
            resolved.Duplicate ??= WindowDuplicatePolicy.Allow;
            resolved.KeepOnScreenChange ??= false;
            resolved.KeepInstance ??= false;
            return resolved;
        }
    }
}
