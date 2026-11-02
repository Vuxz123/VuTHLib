using System;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Computes the canvas sorting order of a window from its <see cref="WindowType"/> and its position in the
    /// window stack. Pure C# so the layering rules can be unit tested.
    /// </summary>
    public static class WindowSortingOrder
    {
        /// <summary>Offset added on top of the system base order for <see cref="WindowType.Tutorial"/> windows.</summary>
        public const int TutorialOffset = 1000;

        /// <summary>
        /// Sorting order = base order for the window type + (stack index * step), where the stack index is
        /// <paramref name="stackCountAfterPush"/> - 1 (clamped to 0).
        /// </summary>
        /// <param name="windowType">Type of the window being shown.</param>
        /// <param name="stackCountAfterPush">Window count AFTER the window was pushed.</param>
        /// <param name="popupBaseSortingOrder">Base order for Popup / FullScreenPopup (and unknown types).</param>
        /// <param name="systemBaseSortingOrder">Base order for System (Tutorial uses this + <see cref="TutorialOffset"/>).</param>
        /// <param name="sortingOrderStep">Order increment per stack index.</param>
        public static int Compute(WindowType windowType, int stackCountAfterPush,
            int popupBaseSortingOrder, int systemBaseSortingOrder, int sortingOrderStep)
        {
            var baseOrder = windowType switch
            {
                WindowType.FullScreenPopup or WindowType.Popup => popupBaseSortingOrder,
                WindowType.System => systemBaseSortingOrder,
                WindowType.Tutorial => systemBaseSortingOrder + TutorialOffset,
                _ => popupBaseSortingOrder
            };

            var index = Math.Max(0, stackCountAfterPush - 1);
            return baseOrder + (index * sortingOrderStep);
        }
    }
}
