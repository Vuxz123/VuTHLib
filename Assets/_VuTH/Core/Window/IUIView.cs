using UnityEngine;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Base interface for all UI views. Showing, hiding and transitions are driven by <see cref="IWindowManager"/>.
    /// </summary>
    public interface IUIView
    {
        GameObject GameObject { get; }
        Canvas Canvas { get; }
        CanvasGroup CanvasGroup { get; }

        void Setup(object data);
        void OnBackPressed(); // Android back button / Escape
    }
}
