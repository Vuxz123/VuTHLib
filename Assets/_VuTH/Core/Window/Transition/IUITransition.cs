using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Reusable transition animations
    /// </summary>
    public interface IUITransition
    {
        UniTask In(IUIView view);
        UniTask Out(IUIView view);
        float Duration { get; }
    }
}