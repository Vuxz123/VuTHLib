namespace _VuTH.Core.Window
{
    /// <summary>
    /// Full screen window - covers everything below it (settings, inventory, shop). Once it is shown, the windows below
    /// stop rendering (their canvases are disabled) and <see cref="WindowStackState.IsScreenCovered"/> becomes true, so
    /// the game can stop rendering the Screen too.
    /// </summary>
    public abstract class FullScreenBase : WindowBase
    {
        public override WindowType WindowType => WindowType.FullScreenPopup;
    }

    /// <summary>Full screen window with typed result.</summary>
    public abstract class FullScreenBase<TResult> : FullScreenBase
    {
        protected void Close(TResult result)
        {
            base.Close(result);
        }
    }

    /// <summary>
    /// Full screen window with typed data and result: open it with
    /// <c>windowManager.Open&lt;TWindow, TData, TResult&gt;(data)</c>, and receive the data in <see cref="OnSetup"/>.
    /// </summary>
    public abstract class FullScreenBase<TData, TResult> : FullScreenBase<TResult>, ITypedWindow<TData, TResult>
    {
        public sealed override void Setup(object data)
        {
            OnSetup(TypedWindowData.Cast<TData>(this, data));
        }

        /// <summary>Receives the data passed to Open, before the window is shown.</summary>
        protected virtual void OnSetup(TData data) { }
    }
}
