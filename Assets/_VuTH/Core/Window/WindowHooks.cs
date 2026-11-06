namespace _VuTH.Core.Window
{
    /// <summary>
    /// A component inside a window that consumes the back button before the window does (e.g. a tab group going back
    /// to its previous tab). Checked by <see cref="UIViewBase.OnBackPressed"/> on the window's active children.
    /// </summary>
    public interface IWindowBackHandler
    {
        /// <summary>Returns true when the back press was consumed.</summary>
        bool HandleBack();
    }

    /// <summary>
    /// Marks a window as a non-modal overlay (e.g. <see cref="ToastBase"/>): it opens and closes outside the operation
    /// queue (it never waits for, nor delays, other windows), it is not the <see cref="IWindowManager.TopWindow"/>, never
    /// takes the back press, and does not hold back queued windows. Other System windows, such as a loading overlay,
    /// are modal.
    /// </summary>
    public interface IWindowOverlay
    {
    }

    /// <summary>A component inside a window notified before each opening, before <see cref="UIViewBase.Setup"/>.</summary>
    public interface IWindowOpenHandler
    {
        void OnWindowOpening();
    }
}
