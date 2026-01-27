using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Window.Transition
{
    public interface IUITransitionRunner
    {
        UniTask RunIn(IUIView view, IUITransition transition);
        UniTask RunOut(IUIView view, IUITransition transition);
    }
}
