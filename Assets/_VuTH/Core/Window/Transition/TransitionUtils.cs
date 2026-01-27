using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    internal static class TransitionUtils
    {
        public static RectTransform GetTransitionRoot(IUIView view)
        {
            if (view is ITransitionTarget target && target.TransitionRoot)
                return target.TransitionRoot;

            return view.GameObject.GetComponent<RectTransform>();
        }
    }
}

