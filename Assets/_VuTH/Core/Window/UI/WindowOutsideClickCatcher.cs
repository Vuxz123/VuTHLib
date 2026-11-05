using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _VuTH.Core.Window.UI
{
    /// <summary>
    /// Added by <see cref="PopupBase"/> on its outside-click area (dimmer): reports clicks on it.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class WindowOutsideClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }
}
