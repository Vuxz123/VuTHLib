using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace _VuTH.Common.Editor.UI.PreviewObject
{
    /// <summary>
    /// Draws an object preview (picture + name) inside a rect, for custom inspectors and editor windows.
    /// <list type="bullet">
    /// <item>Textures, sprites, UI <see cref="Image"/> / <see cref="RawImage"/>, <see cref="SpriteRenderer"/> and
    /// <see cref="Renderer"/> are drawn directly from their texture (sprites with their own sub-rect, aspect kept).</item>
    /// <item>Other assets (prefabs, meshes, materials…) use <see cref="AssetPreview"/>. Unity generates those
    /// asynchronously: while they load, the type icon is shown and the editor is repainted until they are ready.</item>
    /// </list>
    /// Nothing is cached here: <see cref="AssetPreview"/> keeps its own cache, and resolving every draw keeps previews
    /// in sync when a component's sprite / material changes.
    /// </summary>
    public static class PreviewObjectDrawer
    {
        private const float CheckerTileSize = 6f;
        private const double RepaintInterval = 0.2;

        private static readonly Rect FullUv = new(0f, 0f, 1f, 1f);
        private static readonly Color DarkBackground = new(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color LightBackground = new(0.82f, 0.82f, 0.82f, 1f);
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private static bool _repaintScheduled;
        private static double _lastRepaintTime;
        private static Texture2D _checkerTexture;
        private static bool _checkerIsProSkin;
        private static GUIStyle _centeredLabel;

        /// <summary>Draws a checkerboard behind previews so transparent pixels are visible.</summary>
        public static bool ShowCheckerboard { get; set; }

        /// <summary>
        /// Draws a preview of <paramref name="obj"/> inside <paramref name="rect"/> (only during Repaint).
        /// When the rect is taller than wide, the name goes below the picture; otherwise to its right.
        /// </summary>
        public static void DrawObjectPreview<T>(Rect rect, T obj) where T : Object
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var content = EditorGUIUtility.ObjectContent(obj, typeof(T));
            var isTall = CalculateLayout(rect, out var iconRect, out var labelRect);

            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? DarkBackground : LightBackground);

            var source = Resolve(obj);
            if (source.IsPending) ScheduleRepaint();

            if (iconRect.width > 0f && iconRect.height > 0f)
            {
                if (source.Texture) DrawTexture(iconRect, source.Texture, source.Uv);
                else if (content.image) DrawTexture(iconRect, content.image, FullUv);
                else DrawPlaceholder(iconRect);
            }

            if (labelRect.width > 0f && labelRect.height > 0f)
            {
                GUI.Label(labelRect, content.text ?? "None", isTall ? CenteredLabel : EditorStyles.label);
            }
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

        // ---- Layout & drawing ----

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

        /// <summary>Splits <paramref name="rect"/> into a square icon area and a label area. Returns true for the tall layout.</summary>
        private static bool CalculateLayout(Rect rect, out Rect iconRect, out Rect labelRect)
        {
            var isTall = rect.height > rect.width;
            var refSize = Mathf.Min(rect.width, rect.height);
            var iconSize = refSize * 0.8f;
            var padding = (refSize - iconSize) * 0.5f;

            if (isTall)
            {
                // Icon centered at the top, label below.
                iconRect = new Rect(rect.x + (rect.width - iconSize) * 0.5f, rect.y + padding, iconSize, iconSize);
                var textY = iconRect.yMax + padding;
                labelRect = new Rect(rect.x + padding, textY,
                    Mathf.Max(0f, rect.width - 2f * padding), Mathf.Max(0f, rect.yMax - textY - padding));
            }
            else
            {
                // Icon on the left, label on the right.
                iconRect = new Rect(rect.x + padding, rect.y + padding, iconSize, iconSize);
                labelRect = new Rect(iconRect.xMax + padding, rect.y,
                    Mathf.Max(0f, rect.width - iconSize - 3f * padding), rect.height);
            }

            return isTall;
        }

        private static void DrawTexture(Rect area, Texture texture, Rect uv)
        {
            var drawRect = FitToAspect(area, texture.width * uv.width, texture.height * uv.height);
            if (ShowCheckerboard) DrawCheckerboard(drawRect);
            GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
        }

        private static void DrawPlaceholder(Rect rect)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = oldColor;
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

        private static GUIStyle CenteredLabel =>
            _centeredLabel ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.UpperCenter };

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
