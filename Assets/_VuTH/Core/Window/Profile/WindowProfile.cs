using UnityEngine;

namespace _VuTH.Core.Window.Profile
{
    /// <summary>
    /// Window manager settings (Resources/<see cref="WindowManagerConst.WindowProfilePath"/>).
    /// </summary>
    public class WindowProfile : ScriptableObject
    {
        [Header("Canvas Sorting")]
        [Tooltip("Sorting order of the bottom Popup / FullScreenPopup window.")]
        public int popupBaseSortingOrder = 1000;

        [Tooltip("Sorting order of the bottom System window; Tutorial windows add 1000.")]
        public int systemBaseSortingOrder = 2000;

        [Tooltip("Added per window below in the stack, so the newest window is on top.")]
        [Min(1)] public int sortingOrderStep = 10;

        [Header("Input Blocking")]
        [Tooltip("Block all UI input while a window transitions in or out (windows may opt out with BlockInput).")]
        public bool blockInputDuringTransitions = true;
    }
}
