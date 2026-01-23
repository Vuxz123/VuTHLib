using UnityEngine;

namespace Core.Window.Transition
{
    /// <summary>
    /// Optional capability: allows a view to specify which RectTransform should be animated during transitions.
    /// If not implemented, transitions should fall back to view.GameObject's RectTransform.
    /// </summary>
    public interface ITransitionTarget
    {
        RectTransform TransitionRoot { get; }
    }
}
