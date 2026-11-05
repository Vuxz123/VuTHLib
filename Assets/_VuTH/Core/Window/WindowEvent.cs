using _VuTH.Common.MessagePipe.Attributes;

namespace _VuTH.Core.Window
{
    public enum WindowEventType
    {
        Opened,
        Closed,
    }

    /// <summary>Why a window closed.</summary>
    public enum WindowCloseCause
    {
        /// <summary>Closed by the window or the game (Close / CloseTop), with or without a result.</summary>
        Closed,

        /// <summary>Closed by the back button / Escape.</summary>
        BackPress,

        /// <summary>Closed by CloseAll or CloseForScreenChangeAsync.</summary>
        CloseAll,

        /// <summary>ForceClose, destroyed while open, or failed to open.</summary>
        Forced,

        /// <summary>The Open call was canceled.</summary>
        Canceled,

        /// <summary>Replaced by a new window of the same type (<see cref="WindowDuplicatePolicy.ReplaceExisting"/>).</summary>
        Replaced,
    }

    /// <summary>
    /// Published when a window is shown or closed, through MessagePipe (<c>ISubscriber&lt;WindowEvent&gt;</c>) and
    /// <see cref="IWindowManager.OnWindowEvent"/>. For analytics and tutorials.
    /// </summary>
    [MessagePipeEvent]
    public class WindowEvent
    {
        public WindowEventType Type { get; set; }

        /// <summary>Class name of the window.</summary>
        public string WindowName { get; set; } = string.Empty;

        public WindowType WindowType { get; set; }

        /// <summary>The window, if not destroyed.</summary>
        public UIViewBase Window { get; set; }

        /// <summary>Closed only: seconds (unscaled) the window was shown.</summary>
        public float ShownSeconds { get; set; }

        /// <summary>Closed only.</summary>
        public WindowCloseCause CloseCause { get; set; }

        /// <summary>Closed only: whether it was closed with a result.</summary>
        public bool HasResult { get; set; }
    }
}
