using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.GameCycle.Screen.GlobalEvent
{
    [CreateAssetMenu(fileName = "ScreenEventProfile", menuName = "Screen/Screen Event Profile")]
    public class GlobalScreenEventProfile : ScriptableObject, IGlobalScreenEventRegistration
    {
        // [QUAN TRỌNG] SerializeReference cho phép lưu các class con implement interface
        // HideInInspector để ta tự vẽ bằng Custom Editor cho đẹp
        [SerializeReference, HideInInspector] 
        public List<IScreenEventListener> configuredListeners = new();

        // List dùng cho Runtime (bao gồm cả configured + runtime register)
        private List<IScreenEventListener> _runtimeListeners = new();

        private void OnEnable()
        {
            // Reset runtime list khi Play
            _runtimeListeners.Clear();
            if (configuredListeners != null)
            {
                _runtimeListeners.AddRange(configuredListeners);
            }
        }

        #region IGlobalScreenEventRegistration

        public void RegisterListener(IScreenEventListener listener)
        {
            if (!_runtimeListeners.Contains(listener))
            {
                _runtimeListeners.Add(listener);
            }
        }

        public void UnregisterListener(IScreenEventListener listener)
        {
            if (_runtimeListeners.Contains(listener))
            {
                _runtimeListeners.Remove(listener);
            }
        }

        #endregion

        #region Notify Logic

        public void NotifyPreScreenEnter(ScreenEventArgs eventArgs)
        {
            // Iterate ngược để an toàn nếu list bị sửa đổi trong lúc loop
            for (int i = _runtimeListeners.Count - 1; i >= 0; i--)
            {
                _runtimeListeners[i]?.OnPreScreenEnter(eventArgs);
            }
        }

        public void NotifyPostScreenEnter(ScreenEventArgs eventArgs)
        {
            for (int i = _runtimeListeners.Count - 1; i >= 0; i--)
            {
                _runtimeListeners[i]?.OnPostScreenEnter(eventArgs);
            }
        }

        public void NotifyPreScreenExit(ScreenEventArgs eventArgs)
        {
            for (int i = _runtimeListeners.Count - 1; i >= 0; i--)
            {
                _runtimeListeners[i]?.OnPreScreenExit(eventArgs);
            }
        }

        public void NotifyPostScreenExit(ScreenEventArgs eventArgs)
        {
            for (int i = _runtimeListeners.Count - 1; i >= 0; i--)
            {
                _runtimeListeners[i]?.OnPostScreenExit(eventArgs);
            }
        }

        #endregion
    }
}