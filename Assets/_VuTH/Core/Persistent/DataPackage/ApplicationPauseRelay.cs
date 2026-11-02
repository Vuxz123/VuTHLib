using System;
using UnityEngine;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Unity has no static "paused" event (only <see cref="Application.focusChanged"/> and
    /// <see cref="Application.quitting"/>), and on Android/iOS <c>quitting</c> is usually NOT raised when the OS
    /// kills a backgrounded app. <c>OnApplicationPause(true)</c> is the last reliable callback, so this hidden
    /// relay forwards it to plain C# listeners such as <see cref="SaveLifecycleHook"/>.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class ApplicationPauseRelay : MonoBehaviour
    {
        public event Action<bool> PauseChanged;

        public static ApplicationPauseRelay Create()
        {
            var go = new GameObject("[VuTH] ApplicationPauseRelay") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            if (Application.isPlaying) DontDestroyOnLoad(go);
            return go.AddComponent<ApplicationPauseRelay>();
        }

        private void OnApplicationPause(bool paused) => PauseChanged?.Invoke(paused);
    }
}
