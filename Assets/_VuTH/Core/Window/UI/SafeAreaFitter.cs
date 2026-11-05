using UnityEngine;

namespace _VuTH.Core.Window.UI
{
    /// <summary>
    /// Fits a RectTransform to the device safe area (notch, rounded corners, home indicator). Put it on a full-screen
    /// child of a screen-space canvas and the window content under it; keep backgrounds and dimmers outside.
    /// Follows rotation and resolution changes.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool top = true;
        [SerializeField] private bool bottom = true;
        [SerializeField] private bool left = true;
        [SerializeField] private bool right = true;

        private Rect _appliedArea;
        private Vector2Int _appliedScreen;

        private void OnEnable()
        {
            Apply(force: true);
        }

        private void Update()
        {
            Apply(force: false);
        }

        private void Apply(bool force)
        {
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == _appliedArea && screen == _appliedScreen) return;
            if (screen.x <= 0 || screen.y <= 0) return;

            _appliedArea = area;
            _appliedScreen = screen;

            var rect = (RectTransform)transform;
            rect.anchorMin = ComputeAnchorMin(area, screen, left, bottom);
            rect.anchorMax = ComputeAnchorMax(area, screen, right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Normalized lower-left anchor of the safe area; 0 on the edges that are not fitted.</summary>
        public static Vector2 ComputeAnchorMin(Rect safeArea, Vector2Int screen, bool fitLeft, bool fitBottom)
        {
            return new Vector2(
                fitLeft ? safeArea.xMin / screen.x : 0f,
                fitBottom ? safeArea.yMin / screen.y : 0f);
        }

        /// <summary>Normalized upper-right anchor of the safe area; 1 on the edges that are not fitted.</summary>
        public static Vector2 ComputeAnchorMax(Rect safeArea, Vector2Int screen, bool fitRight, bool fitTop)
        {
            return new Vector2(
                fitRight ? safeArea.xMax / screen.x : 1f,
                fitTop ? safeArea.yMax / screen.y : 1f);
        }
    }
}
