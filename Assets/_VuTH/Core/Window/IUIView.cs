using Core.Window.Transition;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Window
{
    /// <summary>
    /// Base interface for all UI controllers
    /// </summary>
    public interface IUIView
    {
        GameObject GameObject { get; }
        Canvas Canvas { get; }
        CanvasGroup CanvasGroup { get; }
        
        void Setup(object data);
        UniTask Show(IUITransition transition = null);
        UniTask Hide(IUITransition transition = null);
        void OnBackPressed(); // Android back button
    }
}