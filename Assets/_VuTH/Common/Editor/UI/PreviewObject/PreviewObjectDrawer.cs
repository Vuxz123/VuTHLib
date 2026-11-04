using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace _VuTH.Common.Editor.UI.PreviewObject
{
    /// <summary>
    /// Draws an object preview (picture + name) inside a rect, for custom inspectors and editor windows.
    /// The layout adapts to the rect:
    /// <list type="bullet">
    /// <item><b>Inline</b> (single line, &lt; 30px high): looks like an object field — small icon + name.</item>
    /// <item><b>Row</b> (wide): square thumbnail on the left, name and type on the right.</item>
    /// <item><b>Card</b> (square-ish / tall): thumbnail filling the card, name as a caption below.</item>
    /// </list>
    /// Textures, sprites, UI <see cref="Image"/> / <see cref="RawImage"/>, <see cref="SpriteRenderer"/> and
    /// <see cref="Renderer"/> are drawn from their texture (sprites with their own sub-rect, aspect kept). Other assets
    /// use <see cref="AssetPreview"/>, generated asynchronously by Unity: the type icon is shown and the editor is
    /// repainted until it is ready. Nothing is cached here (<see cref="AssetPreview"/> has its own cache), so previews
    /// follow changes to a component's sprite / material immediately.
    /// </summary>
    public static class PreviewObjectDrawer
    {
        // Layout
        internal const float InlineMaxHeight = 30f;
        private const float InlinePadding = 2f;
        private const float InlineIconSize = 16f;
        private const float Padding = 4f;
        private const float RowMinAspect = 1.6f;
        private const float RowTextSpacing = 6f;
        private const float TitleHeight = 16f;
        private const float SubtitleHeight = 14f;
        private const float CaptionHeight = 14f;
        private const float CaptionSpacing = 2f;
        private const float MinThumbnailSize = 16f;
        private const float MinTextWidth = 24f;
        private const float TypeIconMaxSize = 32f;

        // Look
        private const float CornerRadius = 3f;
        private const float ThumbnailCornerRadius = 2f;
        private const float EmptyAlpha = 0.45f;
        private const float CheckerTileSize = 6f;

        private const double RepaintInterval = 0.2;

        private static readonly Rect FullUv = new(0f, 0f, 1f, 1f);
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private static bool _repaintScheduled;
        private static double _lastRepaintTime;
        private static Texture2D _checkerTexture;
        private static bool _checkerIsProSkin;

        /// <summary>Draws a checkerboard behind previews so transparent pixels are visible.</summary>
        public static bool ShowCheckerboard { get; set; }

        /// <summary>
        /// Draws a preview of <paramref name="obj"/> inside <paramref name="rect"/> (only during Repaint).
        /// <typeparamref name="T"/> names the expected type when <paramref name="obj"/> is empty ("None (Sprite)").
        /// </summary>
        public static void DrawObjectPreview<T>(Rect rect, T obj) where T : Object
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (rect.width < 1f || rect.height < 1f) return;

            var layout = CalculateLayout(rect);
            var hasValue = (bool)obj;

            DrawRounded(rect, Colors.Frame, 0f, CornerRadius);
            DrawRounded(rect, Colors.Border, 1f, CornerRadius);
            if (layout.Mode != PreviewLayoutMode.Inline)
            {
                DrawRounded(layout.Thumbnail, Colors.Well, 0f, ThumbnailCornerRadius);
            }

            DrawPicture(layout, obj, hasValue, typeof(T));
            DrawTexts(layout, obj, hasValue, typeof(T));
        }

        // ---- Layout (internal for tests) ----

        internal enum PreviewLayoutMode
        {
            Inline,
            Row,
            Card,
        }

        /// <summary>Where each part goes. Hidden parts have a zero rect.</summary>
        internal readonly struct PreviewLayout
        {
            public readonly PreviewLayoutMode Mode;
            public readonly Rect Thumbnail;
            public readonly Rect Title;
            public readonly Rect Subtitle;

            public PreviewLayout(PreviewLayoutMode mode, Rect thumbnail, Rect title, Rect subtitle)
            {
                Mode = mode;
                Thumbnail = thumbnail;
                Title = title;
                Subtitle = subtitle;
            }
        }

        internal static PreviewLayout CalculateLayout(Rect rect)
        {
            if (rect.height < InlineMaxHeight) return InlineLayout(rect);

            var inner = Shrink(rect, Padding);
            return rect.width >= rect.height * RowMinAspect ? RowLayout(inner) : CardLayout(inner);
        }

        private static PreviewLayout InlineLayout(Rect rect)
        {
            var size = Mathf.Max(0f, Mathf.Min(InlineIconSize, rect.height - 2f * InlinePadding));
            var icon = new Rect(rect.x + InlinePadding + 1f, rect.center.y - size * 0.5f, size, size);
            var title = Rect.MinMaxRect(icon.xMax + 3f, rect.y, rect.xMax - InlinePadding, rect.yMax);
            if (title.width < MinTextWidth) title = Rect.zero;

            return new PreviewLayout(PreviewLayoutMode.Inline, icon, title, Rect.zero);
        }

        private static PreviewLayout RowLayout(Rect inner)
        {
            var size = inner.height;
            var textX = inner.x + size + RowTextSpacing;
            var textWidth = inner.xMax - textX;

            if (textWidth < MinTextWidth)
            {
                // No room for text: just the thumbnail, centered.
                var centered = new Rect(inner.center.x - size * 0.5f, inner.y, size, size);
                return new PreviewLayout(PreviewLayoutMode.Row, centered, Rect.zero, Rect.zero);
            }

            var thumbnail = new Rect(inner.x, inner.y, size, size);
            if (inner.height < TitleHeight + SubtitleHeight)
            {
                var titleOnly = new Rect(textX, inner.y, textWidth, inner.height);
                return new PreviewLayout(PreviewLayoutMode.Row, thumbnail, titleOnly, Rect.zero);
            }

            // Name + type, vertically centered as one block.
            var top = inner.center.y - (TitleHeight + SubtitleHeight) * 0.5f;
            var title = new Rect(textX, top, textWidth, TitleHeight);
            var subtitle = new Rect(textX, title.yMax, textWidth, SubtitleHeight);
            return new PreviewLayout(PreviewLayoutMode.Row, thumbnail, title, subtitle);
        }

        private static PreviewLayout CardLayout(Rect inner)
        {
            var thumbnailHeight = inner.height - CaptionHeight - CaptionSpacing;
            var showCaption = thumbnailHeight >= MinThumbnailSize && inner.width >= MinTextWidth;
            if (!showCaption) return new PreviewLayout(PreviewLayoutMode.Card, inner, Rect.zero, Rect.zero);

            var thumbnail = new Rect(inner.x, inner.y, inner.width, thumbnailHeight);
            var caption = new Rect(inner.x, thumbnail.yMax + CaptionSpacing, inner.width, CaptionHeight);
            return new PreviewLayout(PreviewLayoutMode.Card, thumbnail, caption, Rect.zero);
        }

        private static Rect Shrink(Rect rect, float amount) =>
            new(rect.x + amount, rect.y + amount,
                Mathf.Max(0f, rect.width - 2f * amount), Mathf.Max(0f, rect.height - 2f * amount));

        /// <summary>Largest rect with the content's aspect ratio that fits <paramref name="area"/>, centered.</summary>
        internal static Rect FitToAspect(Rect area, float contentWidth, float contentHeight)
        {
            contentWidth = Mathf.Abs(contentWidth);
            contentHeight = Mathf.Abs(contentHeight);
            if (contentWidth <= 0f || contentHeight <= 0f || area.width <= 0f || area.height <= 0f) return area;

            var aspect = contentWidth / contentHeight;
            var width = area.width;
            var height = width / aspect;
            if (height > area.height)
            {
                height = area.height;
                width = height * aspect;
            }

            return new Rect(area.x + (area.width - width) * 0.5f, area.y + (area.height - height) * 0.5f, width, height);
        }

        // ---- Drawing ----

        private static void DrawPicture(in PreviewLayout layout, Object obj, bool hasValue, System.Type expectedType)
        {
            if (layout.Thumbnail.width < 1f || layout.Thumbnail.height < 1f) return;

            var source = Resolve(obj);
            if (source.IsPending) ScheduleRepaint();

            if (source.Texture)
            {
                DrawImage(layout.Thumbnail, source.Texture, source.Uv);
                return;
            }

            // No picture (yet): the type icon, kept at its natural size so it is not blown up blurry.
            var icon = hasValue
                ? AssetPreview.GetMiniThumbnail(obj)
                : EditorGUIUtility.ObjectContent(null, expectedType).image;
            if (icon) DrawIcon(layout.Thumbnail, icon, hasValue ? 1f : EmptyAlpha);
        }

        private static void DrawTexts(in PreviewLayout layout, Object obj, bool hasValue, System.Type expectedType)
        {
            if (layout.Title.width < 1f) return;

            var typeName = ObjectNames.NicifyVariableName((hasValue ? obj.GetType() : expectedType).Name);
            var isRow = layout.Mode == PreviewLayoutMode.Row;
            var title = hasValue ? obj.name : isRow ? "None" : $"None ({typeName})";

            var titleStyle = layout.Mode switch
            {
                PreviewLayoutMode.Inline => Styles.Inline,
                PreviewLayoutMode.Row => Styles.RowTitle,
                _ => Styles.Caption,
            };

            var oldColor = GUI.contentColor;
            if (!hasValue) GUI.contentColor = new Color(1f, 1f, 1f, EmptyAlpha + 0.15f);

            GUI.Label(layout.Title, title, titleStyle);
            if (layout.Subtitle.width >= 1f) GUI.Label(layout.Subtitle, typeName, Styles.Subtitle);

            GUI.contentColor = oldColor;
        }

        private static void DrawImage(Rect area, Texture texture, Rect uv)
        {
            var drawRect = FitToAspect(area, texture.width * uv.width, texture.height * uv.height);
            if (ShowCheckerboard) DrawCheckerboard(drawRect);
            GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
        }

        private static void DrawIcon(Rect area, Texture icon, float alpha)
        {
            var size = Mathf.Min(area.width, area.height, TypeIconMaxSize);
            var rect = new Rect(area.center.x - size * 0.5f, area.center.y - size * 0.5f, size, size);
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true, 0f, new Color(1f, 1f, 1f, alpha), 0f, 0f);
        }

        /// <summary>Filled (<paramref name="borderWidth"/> = 0) or outlined rounded rect.</summary>
        private static void DrawRounded(Rect rect, Color color, float borderWidth, float radius)
        {
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0f, color,
                borderWidth, radius);
        }

        private static void DrawCheckerboard(Rect rect)
        {
            if (!_checkerTexture || _checkerIsProSkin != EditorGUIUtility.isProSkin)
            {
                CreateCheckerTexture();
            }

            // One texture tile = 2x2 checker cells; repeat it across the rect.
            var tiles = new Rect(0f, 0f, rect.width / (CheckerTileSize * 2f), rect.height / (CheckerTileSize * 2f));
            GUI.DrawTextureWithTexCoords(rect, _checkerTexture, tiles, false);
        }

        private static void CreateCheckerTexture()
        {
            if (_checkerTexture) Object.DestroyImmediate(_checkerTexture);

            _checkerIsProSkin = EditorGUIUtility.isProSkin;
            var dark = _checkerIsProSkin ? new Color(0.3f, 0.3f, 0.3f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
            var light = _checkerIsProSkin ? new Color(0.4f, 0.4f, 0.4f, 1f) : new Color(0.9f, 0.9f, 0.9f, 1f);

            _checkerTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };
            _checkerTexture.SetPixels(new[] { dark, light, light, dark });
            _checkerTexture.Apply();
        }

        /// <summary>Theme colors, matching Unity's own field / well colors for the current skin.</summary>
        private static class Colors
        {
            private static bool Pro => EditorGUIUtility.isProSkin;

            public static Color Frame => Pro ? new Color(0.165f, 0.165f, 0.165f) : new Color(0.94f, 0.94f, 0.94f);
            public static Color Border => Pro ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.62f, 0.62f, 0.62f);
            public static Color Well => Pro ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.86f, 0.86f, 0.86f);
            public static Color Secondary => Pro ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.38f, 0.38f, 0.38f);
        }

        /// <summary>Text styles, rebuilt when the editor skin changes.</summary>
        private static class Styles
        {
            private static bool _builtForProSkin;
            private static GUIStyle _inline, _rowTitle, _subtitle, _caption;

            public static GUIStyle Inline => Get(ref _inline);
            public static GUIStyle RowTitle => Get(ref _rowTitle);
            public static GUIStyle Subtitle => Get(ref _subtitle);
            public static GUIStyle Caption => Get(ref _caption);

            private static GUIStyle Get(ref GUIStyle style)
            {
                if (_inline == null || _builtForProSkin != EditorGUIUtility.isProSkin) Build();
                return style;
            }

            private static void Build()
            {
                _builtForProSkin = EditorGUIUtility.isProSkin;

                _inline = SingleLine(EditorStyles.label, TextAnchor.MiddleLeft);
                _rowTitle = SingleLine(EditorStyles.boldLabel, TextAnchor.MiddleLeft);
                _subtitle = SingleLine(EditorStyles.miniLabel, TextAnchor.MiddleLeft);
                _subtitle.normal.textColor = Colors.Secondary;
                _caption = SingleLine(EditorStyles.miniLabel, TextAnchor.MiddleCenter);
            }

            private static GUIStyle SingleLine(GUIStyle source, TextAnchor alignment) => new(source)
            {
                alignment = alignment,
                clipping = TextClipping.Ellipsis,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
        }

        // ---- Resolution (internal for tests) ----

        /// <summary>A texture and the normalized sub-rect of it to draw.</summary>
        internal readonly struct PreviewSource
        {
            public readonly Texture Texture;
            public readonly Rect Uv;

            /// <summary>An <see cref="AssetPreview"/> is still being generated: draw the fallback and repaint later.</summary>
            public readonly bool IsPending;

            public PreviewSource(Texture texture, Rect uv, bool isPending = false)
            {
                Texture = texture;
                Uv = uv;
                IsPending = isPending;
            }

            public PreviewSource AsPending() => new(Texture, Uv, true);
        }

        internal static PreviewSource Resolve(Object obj)
        {
            if (!obj) return default;

            return obj switch
            {
                Texture texture => new PreviewSource(texture, FullUv),
                Sprite sprite => ResolveSprite(sprite),
                Image image => image.sprite ? ResolveSprite(image.sprite) : default,
                RawImage raw => raw.texture ? new PreviewSource(raw.texture, raw.uvRect) : default,
                SpriteRenderer spriteRenderer => spriteRenderer.sprite ? ResolveSprite(spriteRenderer.sprite) : default,
                Renderer renderer => FromMaterial(renderer.sharedMaterial),
                GameObject go when !EditorUtility.IsPersistent(go) => ResolveFromComponents(go),
                _ => EditorUtility.IsPersistent(obj) ? ResolveAsset(obj) : default,
            };
        }

        private static PreviewSource ResolveSprite(Sprite sprite)
        {
            var texture = sprite.texture;

            // textureRect throws for tightly packed sprites, and rotated ones cannot be drawn with a plain sub-rect.
            var cannotUseRect = sprite.packed &&
                                (sprite.packingMode == SpritePackingMode.Tight ||
                                 sprite.packingRotation != SpritePackingRotation.None);

            if (!texture || cannotUseRect)
            {
                return EditorUtility.IsPersistent(sprite) ? ResolveAsset(sprite) : default;
            }

            return new PreviewSource(texture, SpriteUv(sprite.textureRect, texture.width, texture.height));
        }

        private static PreviewSource ResolveAsset(Object asset)
        {
            var preview = AssetPreview.GetAssetPreview(asset);
            if (preview) return new PreviewSource(preview, FullUv);

            // Not generated yet (or not available for this type): show a direct texture if there is one meanwhile.
            var fallback = asset switch
            {
                Material material => FromMaterial(material),
                GameObject go => ResolveFromComponents(go),
                _ => default,
            };

            return AssetPreview.IsLoadingAssetPreviews() ? fallback.AsPending() : fallback;
        }

        private static PreviewSource ResolveFromComponents(GameObject go)
        {
            if (go.TryGetComponent(out Image image)) return Resolve(image);
            if (go.TryGetComponent(out RawImage raw)) return Resolve(raw);

            var spriteRenderer = go.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer && spriteRenderer.sprite) return ResolveSprite(spriteRenderer.sprite);

            var renderer = go.GetComponentInChildren<Renderer>(true);
            return renderer ? FromMaterial(renderer.sharedMaterial) : default;
        }

        private static PreviewSource FromMaterial(Material material)
        {
            if (!material) return default;

            // Checked first: reading a missing texture property logs an error.
            Texture texture = null;
            if (material.HasTexture(BaseMapId)) texture = material.GetTexture(BaseMapId);
            if (!texture && material.HasTexture(MainTexId)) texture = material.GetTexture(MainTexId);

            return texture ? new PreviewSource(texture, FullUv) : default;
        }

        /// <summary>Normalized (0..1) rect of a sprite region inside its texture.</summary>
        internal static Rect SpriteUv(Rect textureRect, float textureWidth, float textureHeight)
        {
            if (textureWidth <= 0f || textureHeight <= 0f) return FullUv;

            return new Rect(
                textureRect.x / textureWidth,
                textureRect.y / textureHeight,
                textureRect.width / textureWidth,
                textureRect.height / textureHeight);
        }

        // ---- Async previews ----

        /// <summary>Repaints all views (throttled) while asset previews load, then once more when they are done.</summary>
        private static void ScheduleRepaint()
        {
            if (_repaintScheduled) return;
            _repaintScheduled = true;
            EditorApplication.update += RepaintWhileLoading;
        }

        private static void RepaintWhileLoading()
        {
            var loading = AssetPreview.IsLoadingAssetPreviews();
            var now = EditorApplication.timeSinceStartup;
            if (loading && now - _lastRepaintTime < RepaintInterval) return;

            _lastRepaintTime = now;
            InternalEditorUtility.RepaintAllViews();

            if (loading) return;
            EditorApplication.update -= RepaintWhileLoading;
            _repaintScheduled = false;
        }
    }
}
