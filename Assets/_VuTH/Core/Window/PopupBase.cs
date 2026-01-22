
namespace Core.Window
{
    /// <summary>
    /// Modal popup - dialog with dimmed background.
    /// Use for confirmations, alerts, small dialogs.
    /// </summary>
    public abstract class PopupBase : UIViewBase, IWindowDefinition
    {
        public virtual WindowType WindowType => WindowType.Popup;
        public virtual UILayer Layer => UILayer.Popup;
        public virtual string TransitionPreset => "Scale";
        public virtual bool BlockInput => true;

        /// <summary>
        /// Whether this popup can be closed by back button.
        /// Override to false for important confirmations.
        /// </summary>
        public virtual bool AllowBackClose => true;

        public bool CloseOnBackPress => AllowBackClose;
        
        /// <summary>
        /// Whether clicking outside the popup should close it.
        /// </summary>
        public virtual bool CloseOnOutsideClick => false;
        
        public override void OnBackPressed()
        {
            if (AllowBackClose)
            {
                Close();
            }
        }
    }
    
    /// <summary>
    /// Popup with typed result
    /// </summary>
    public abstract class PopupBase<TResult> : PopupBase
    {
        protected void Close(TResult result)
        {
            CloseSource?.TrySetResult(result);
        }
    }
}