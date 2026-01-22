namespace Core.Window
{
    /// <summary>
    /// Window type classification
    /// </summary>
    public enum WindowType
    {
        /// <summary>
        /// Full screen popup - closes previous content, blocks interaction below
        /// </summary>
        FullScreenPopup,
        
        /// <summary>
        /// Modal popup - centered dialog with dimmed background
        /// </summary>
        Popup,
        
        /// <summary>
        /// System overlay - notifications, toasts, loading
        /// </summary>
        System,
        
        /// <summary>
        /// Tutorial overlay - guided tours
        /// </summary>
        Tutorial
    }
}