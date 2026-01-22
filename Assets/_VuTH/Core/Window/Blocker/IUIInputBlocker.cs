namespace Core.Window.Blocker
{
    /// <summary>
    /// Input blocking during transitions
    /// </summary>
    public interface IUIInputBlocker
    {
        void Block(string reason = null);
        void Unblock(string reason = null);
        bool IsBlocked { get; }
    }
}