using System;
using UnityEngine;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Added by <see cref="WindowManager"/> to each window: reports a window destroyed while open (scene unload, Destroy
    /// by game code) so it is removed from the stack and whoever awaits it is released.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class WindowLifetimeWatcher : MonoBehaviour
    {
        public Action Destroyed;

        private void OnDestroy() => Destroyed?.Invoke();
    }
}
