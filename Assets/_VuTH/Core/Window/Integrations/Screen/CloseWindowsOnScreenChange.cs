using System;
using _VuTH.Core.GameCycle.Screen.Events.Global;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Core.Window.ScreenIntegration
{
    /// <summary>
    /// Screen event listener closing the windows that do not keep open on a Screen change
    /// (<see cref="WindowOptions.KeepOnScreenChange"/>) when the current Screen starts exiting. Add it to the global
    /// Screen event profile. Lives in its own assembly so the Window and Screen modules stay independent.
    /// </summary>
    [Serializable]
    public sealed class CloseWindowsOnScreenChange : IScreenEventListener
    {
        [Tooltip("Close without the windows' out transitions (the screen transition covers them).")]
        [SerializeField] private bool immediate = true;

        [Tooltip("Also cancel the windows still waiting in the queue.")]
        [SerializeField] private bool clearQueue;

        public void OnPreScreenExit(ScreenEventArgs eventArgs)
        {
            if (!WindowManager.HasInstance) return;

            var manager = WindowManager.Instance;
            if (clearQueue) manager.ClearQueue();
            manager.CloseForScreenChangeAsync(immediate).Forget();
        }

        public void OnPostScreenExit(ScreenEventArgs eventArgs) { }
        public void OnPreScreenEnter(ScreenEventArgs eventArgs) { }
        public void OnPostScreenEnter(ScreenEventArgs eventArgs) { }
    }
}
