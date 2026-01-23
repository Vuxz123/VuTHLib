using Cysharp.Threading.Tasks;

namespace Core.Window.Transition
{
    public interface IUITransitionRunner
    {
        UniTask RunIn(IUIView view, IUITransition transition);
        UniTask RunOut(IUIView view, IUITransition transition);
    }
}
