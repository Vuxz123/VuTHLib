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

    /// <summary>A component inside a window notified before each opening, before <see cref="UIViewBase.Setup"/>.</summary>
    public interface IWindowOpenHandler
    {
        void OnWindowOpening();
    }
}
