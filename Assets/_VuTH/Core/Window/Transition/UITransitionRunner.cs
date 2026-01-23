using Cysharp.Threading.Tasks;

namespace Core.Window.Transition
{
    /// <summary>
    /// Centralized transition execution.
    /// Keeps consistent final UI states even if transition is null.
    /// </summary>
    public sealed class UITransitionRunner : IUITransitionRunner
    {
        public async UniTask RunIn(IUIView view, IUITransition transition)
        {
            if (transition == null)
            {
                // Ensure final state
                var cg = view.CanvasGroup;
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;
                return;
            }

            await transition.In(view);
        }

        public async UniTask RunOut(IUIView view, IUITransition transition)
        {
            if (transition == null)
            {
                // Ensure final state
                var cg = view.CanvasGroup;
                cg.interactable = false;
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                return;
            }

            await transition.Out(view);
        }
    }
}

