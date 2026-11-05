namespace _VuTH.Core.Window
{
    /// <summary>What opening a window does when a window of the same type is already open.</summary>
    public enum WindowDuplicatePolicy
    {
        /// <summary>Open another instance.</summary>
        Allow,

        /// <summary>
        /// Keep the open window; the call shares it (Show returns it, Open completes with its result). Prevents
        /// double-tap duplicates.
        /// </summary>
        Ignore,

        /// <summary>Like <see cref="Ignore"/>, and move the open window to the top of the stack.</summary>
        BringToFront,

        /// <summary>Close the open window (its Open returns the default result), then open a new one.</summary>
        ReplaceExisting,
    }
}
