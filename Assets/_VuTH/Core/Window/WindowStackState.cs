namespace _VuTH.Core.Window
{
    /// <summary>
    /// Summary of the window stack, reported by <see cref="IWindowManager.OnStackChanged"/>: pause gameplay while a
    /// modal window is open, stop rendering the Screen while it is covered.
    /// </summary>
    public readonly struct WindowStackState
    {
        /// <summary>Open windows that are not overlays (toasts do not count).</summary>
        public readonly int ModalCount;

        /// <summary>Whether a shown full screen window covers the Screen; the windows below it are not rendered.</summary>
        public readonly bool IsScreenCovered;

        /// <summary>The topmost window that is not an overlay, or null.</summary>
        public readonly UIViewBase Top;

        public bool HasModal => ModalCount > 0;

        public WindowStackState(int modalCount, bool isScreenCovered, UIViewBase top)
        {
            ModalCount = modalCount;
            IsScreenCovered = isScreenCovered;
            Top = top;
        }

        public bool Equals(WindowStackState other) =>
            ModalCount == other.ModalCount && IsScreenCovered == other.IsScreenCovered && ReferenceEquals(Top, other.Top);
    }
}
