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
            ScreenManager.Instance.LocalEventRegistration.RegisterOnScreenClosing(OnSceneUnloaded);
            ScreenManager.Instance.LocalEventRegistration.RegisterOnScreenOpening(OnSceneLoaded);
        }

        private void OnDisable()
        {
            if (!ScreenManager.HasInstance) return;
            ScreenManager.Instance.LocalEventRegistration.UnregisterOnScreenClosing(OnSceneUnloaded);
            ScreenManager.Instance.LocalEventRegistration.UnregisterOnScreenOpening(OnSceneLoaded);
        }

        private void OnSceneLoaded(ScreenEventArgs arg)
        {
            onScreenEnter?.Invoke(arg);
        }

        private void OnSceneUnloaded(ScreenEventArgs arg)
        {
            onScreenExit?.Invoke(arg);
        }
    }
}