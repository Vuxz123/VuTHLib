// Filepath: Assets/_VuTH/Common/Editor/Helpers/CustomEditorStyles.cs

using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Helpers // Hoặc namespace ...Editor.Helpers tùy cậu
{
    /// <summary>
    /// Chứa các GUIStyle tùy chỉnh, được khởi tạo tĩnh để
    /// tái sử dụng trong các script Editor.
    /// </summary>
    public static class CustomEditorStyles
    {
        private static GUIStyle _boldFoldoutStyle;
        private static Texture2D _blackTexture;
        private static GUIStyle _pillBadgeStyle;
        private static Texture2D _pillBackgroundTexture;

        /// <summary>
        /// Style cho Foldout (dropdown) in đậm. Dựa trên EditorStyles.foldout để giữ bố cục chuẩn.
        /// </summary>
        public static GUIStyle BoldFoldoutStyle
        {
            get
            {
                if (_boldFoldoutStyle == null)
                {
                    var baseStyle = EditorStyles.foldout;
                    _boldFoldoutStyle = new GUIStyle
                    {
                        fontStyle = FontStyle.Bold,
                        // Giữ padding gốc để triangle không lệch
                        padding = new RectOffset(
                            baseStyle.padding.left, 
                            baseStyle.padding.right, 
                            baseStyle.padding.top, 
                            baseStyle.padding.bottom
                            ),
                        // Không set background tại đây để tránh phủ nền không mong muốn
                    };
                }
                return _boldFoldoutStyle;
            }
        }

        /// <summary>
        /// 1x1 texture đen (hoặc tối) dùng để vẽ nền tùy chọn (DrawRect / GUI.DrawTexture).
        /// </summary>
        public static Texture2D BlackTexture
        {
            get
            {
                if (_blackTexture != null) return _blackTexture;
                _blackTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _blackTexture.SetPixel(0, 0, EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.80f, 0.80f, 0.80f));
                _blackTexture.Apply();
                return _blackTexture;
            }
        }

        /// <summary>
        /// Style cho badge hình "pill" hiển thị số lượng (Items count). Co giãn theo nội dung.
        /// </summary>
        public static GUIStyle PillBadgeStyle
        {
            get
            {
                if (_pillBadgeStyle == null)
                {
                    // Tạo texture nền 1x1 với màu tuỳ theo theme.
                    if (_pillBackgroundTexture == null)
                    {
                        _pillBackgroundTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                        var col = EditorGUIUtility.isProSkin ? new Color(0.25f, 0.55f, 0.95f, 0.85f) : new Color(0.30f, 0.60f, 0.95f, 0.90f);
                        _pillBackgroundTexture.SetPixel(0, 0, col);
                        _pillBackgroundTexture.Apply();
                    }
                    _pillBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(10, 10, 2, 2),
                        margin = new RectOffset(2, 4, 2, 2),
                        normal = new GUIStyleState
                        {
                            background = _pillBackgroundTexture,
                            textColor = Color.white
                        }
                    };
                }
                return _pillBadgeStyle;
            }
        }

        /// <summary>
        /// Vẽ một header có nền + foldout. Trả về trạng thái mới (mở/đóng).
        /// Cho phép hiển thị thêm số lượng phần tử (countBadge nếu >= 0).
        /// </summary>
        public static bool DrawFoldoutHeader(string title, bool state, GUIStyle backgroundStyle = null, int countBadge = -1)
        {
            if (backgroundStyle == null)
                backgroundStyle = EditorStyles.toolbar; // nền phẳng đẹp cho header

            var rect = GUILayoutUtility.GetRect(0, backgroundStyle.fixedHeight > 0 ? backgroundStyle.fixedHeight : 20f, backgroundStyle, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, backgroundStyle);

            // Compute badge content & size
            GUIContent badgeContent = null;
            Vector2 badgeSize = Vector2.zero;
            if (countBadge >= 0)
            {
                badgeContent = new GUIContent(countBadge.ToString(), "Số lượng phần tử trong dictionary");
                badgeSize = PillBadgeStyle.CalcSize(badgeContent);
                // Clamp minimal width and add some extra horizontal padding margin
                badgeSize.x = Mathf.Max(badgeSize.x, 32f);
            }

            float badgeTotalWidth = (countBadge >= 0) ? (badgeSize.x + 8f) : 0f; // include margin
            var foldoutRect = new Rect(rect.x + 4f, rect.y, rect.width - badgeTotalWidth - 4f, rect.height);
            state = EditorGUI.Foldout(foldoutRect, state, title, true, BoldFoldoutStyle);

            if (countBadge >= 0 && badgeContent != null)
            {
                var badgeRect = new Rect(rect.xMax - badgeSize.x - 4f, rect.y + (rect.height - badgeSize.y) * 0.5f, badgeSize.x, badgeSize.y);
                // Draw pill
                GUI.Label(badgeRect, badgeContent, PillBadgeStyle);
            }
            return state;
        }
    }
}