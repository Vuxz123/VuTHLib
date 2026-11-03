using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Events.Global;
using UnityEngine;
using UnityEngine.Events;

namespace _VuTH.Core.GameCycle.Screen.Events.Local
{
    public class CurrentScreenEventListener : MonoBehaviour
    {
        [SerializeField, ReadOnlyField] private ScreenMetaData screenMetaData;
        
        [Header("Events")]
        #region Screen Events
        public UnityEvent<ScreenEventArgs> onScreenEnter;
        public UnityEvent<ScreenEventArgs> onScreenExit;
        #endregion

        private void OnValidate()
        {
            if (!screenMetaData)
            {
                screenMetaData = gameObject.GetScreenMetaData();
            }
        }

        private void OnEnable()
        {
            if (!screenMetaData)
            {
                this.LogError("Screen Meta Data not assigned!!");
                return;
            }
            if (!ScreenManager.HasInstance)
            {
                this.LogWarning("ScreenManager is not available; screen events will not be received.");
                return;
            }

            ScreenManager.Instance.LocalEventRegistration.RegisterOnScreenClosing(OnSceneUnloaded);
            ScreenManager.Instance.LocalEventRegistration.RegisterOnScreenOpening(OnSceneLoaded);
        }

        private void OnDisable()
        {
            if (!ScreenManager.HasInstance) return;
            ScreenManager.Instance.LocalEventRegistration.UnregisterOnScreenClosing(OnSceneUnloaded);
            ScreenManager.Instance.LocalEventRegistration.UnregisterOnScreenOpening(OnSceneLoaded);
        }

        // Local events are broadcast for every transition; only react to the screen this scene belongs to.
        private void OnSceneLoaded(ScreenEventArgs arg)
        {
            if (!IsOwnScreen(arg.ToScreen)) return;
            onScreenEnter?.Invoke(arg);
        }

        private void OnSceneUnloaded(ScreenEventArgs arg)
        {
            if (!IsOwnScreen(arg.FromScreen)) return;
            onScreenExit?.Invoke(arg);
        }

        private bool IsOwnScreen(IScreenDefinition screen)
        {
            var own = screenMetaData ? screenMetaData.ScreenDefinition : null;
            return own != null && ReferenceEquals(own, screen);
        }
    }
}