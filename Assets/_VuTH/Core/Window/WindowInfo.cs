namespace _VuTH.Core.Window
{
    /// <summary>Phase of an open window.</summary>
    public enum WindowState
    {
        /// <summary>Spawned, set up, transitioning in.</summary>
        Opening,

        /// <summary>Shown.</summary>
        Open,

        /// <summary>Transitioning out.</summary>
        Closing,
    }

    /// <summary>Snapshot of one window of the stack, for debugging tools (<see cref="IWindowManager.GetOpenWindows"/>).</summary>
    public readonly struct WindowInfo
    {
        public readonly UIViewBase Window;
        public readonly WindowType WindowType;
        public readonly int SortingOrder;
        public readonly WindowState State;
        public readonly bool CloseOnBackPress;

        public WindowInfo(UIViewBase window, WindowType windowType, int sortingOrder, WindowState state, bool closeOnBackPress)
        {
            Window = window;
            WindowType = windowType;
            SortingOrder = sortingOrder;
            State = state;
            CloseOnBackPress = closeOnBackPress;
        }
    }
}
