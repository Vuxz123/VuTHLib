using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Common.Editor.UI
{
    /// <summary>
    /// Helper for drawing an object preview (icon + label) in custom inspectors.
    ///
    /// Uses <see cref="AssetPreview"/> when possible, with a small cache keyed by instance ID.
    /// For texture-like objects (Sprite, Texture2D, RenderTexture) it falls back to their
    /// underlying texture when Unity does not provide a generated preview.
    ///
    /// Note: Asset previews are generated asynchronously by Unity. On the first few GUI
    /// events, <see cref="AssetPreview.GetAssetPreview"/> can legitimately return null, and
    /// the texture will "pop in" on later repaints.
    /// </summary>
    public static class PreviewObjectDrawer
    {
        // Toggle this to enable debug logging to the console for troubleshooting preview issues.
        public static bool EnableDebug = false;
        // Toggle to draw a subtle checkerboard behind previews to make transparency visible and avoid
        // the appearance of a black background for textures with alpha or preview rendering quirks.
        public static bool ShowCheckerboard = false;

        // Cache previews by instance ID to avoid repeated AssetPreview calls.
        // Only non-null textures are cached so that we can still pick up a preview later
        // once Unity finishes generating it.
        private static readonly Dictionary<int, Texture> PreviewCache = new Dictionary<int, Texture>();
        // Cache UV rects (0..1) associated with an instance ID when the texture is a sub-region (e.g., Sprite in atlas)
        private static readonly Dictionary<int, Rect> PreviewUVCache = new Dictionary<int, Rect>();

        /// <summary>
        /// Draws a preview for the given object inside <paramref name="rect"/>.
        /// If the rect is taller than wide, the label is placed below the icon, otherwise to the right.
        /// A background matching the editor theme is drawn behind the preview.
        ///
        /// This method is best used with a reasonably sized, non-zero rect. When no preview
        /// is available yet, a subtle placeholder box is drawn instead of leaving the area empty.
        /// </summary>
        public static void DrawObjectPreview<T>(Rect rect, T obj) where T : Object
        {
            // Guard: nothing sensible to draw for zero-extent rects.
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            var content = EditorGUIUtility.ObjectContent(obj, typeof(T));

            // Decide layout based on aspect: if taller than wide, place label below icon; otherwise, label to the right
            bool isTall = rect.height > rect.width;

            float refSize = Mathf.Min(rect.width, rect.height);
            float iconSize = refSize * 0.8f;
            float padding = (refSize - iconSize) / 2f;

            Rect iconRect;
            Rect labelRect = new Rect(0f, 0f, 0f, 0f);

            if (isTall)
            {
                // Center icon horizontally near the top; label occupies space below
                float iconX = rect.x + (rect.width - iconSize) / 2f;
                float iconY = rect.y + padding;
                iconRect = new Rect(iconX, iconY, iconSize, iconSize);

                float textY = iconRect.yMax + padding;
                float textH = Mathf.Max(0f, rect.yMax - textY - padding);
                labelRect = new Rect(rect.x + padding, textY, Mathf.Max(0f, rect.width - 2f * padding), textH);
            }
            else
            {
                // Icon on the left (square), text on the right
                iconRect = new Rect(rect.x + padding, rect.y + padding, iconSize, iconSize);
                float textWidth = Mathf.Max(0f, rect.width - iconSize - 3f * padding);
                labelRect = new Rect(iconRect.xMax + padding, rect.y, textWidth, rect.height);
            }

            // Background: solid theme-matching color
            // Use opaque background (alpha = 1) so the preview area matches the editor theme and
            // does not appear as a black/transparent strip behind the icon.
            Color bg = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.82f, 0.82f, 0.82f, 1f);
            EditorGUI.DrawRect(rect, bg);

            // Resolve a preview texture and UV rect for visual assets using AssetPreview with cache
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            Texture previewTex = ResolvePreviewTextureWithUV(obj, out uv);

            // Fallback to content image if no preview is available
            if (previewTex == null)
            {
                previewTex = content.image;
                uv = new Rect(0f, 0f, 1f, 1f);
            }

            // Debug: report preview found
            if (EnableDebug)
            {
                if (previewTex != null)
                {
                    Debug.Log($"[PreviewObjectDrawer] Found preview texture={previewTex} size=({previewTex.width}x{previewTex.height}) uv={uv} for obj={obj}");
                }
                else
                {
                    Debug.Log($"[PreviewObjectDrawer] No preview for obj={obj}");
                }
            }

            // Draw icon or a subtle placeholder if nothing is available yet.
            if (iconRect.width > 0f && iconRect.height > 0f)
            {
                // Optional: draw a subtle checkerboard beneath the preview to reveal transparency and
                // avoid the black-looking background sometimes produced by preview textures.
                if (ShowCheckerboard)
                {
                    // Small tile size keeps pattern subtle; clamp to rect size.
                    int tile = 6;
                    float w = iconRect.width;
                    float h = iconRect.height;
                    for (int yy = 0; yy < Mathf.CeilToInt(h / tile); yy++)
                    {
                        for (int xx = 0; xx < Mathf.CeilToInt(w / tile); xx++)
                        {
                            Rect t = new Rect(iconRect.x + xx * tile, iconRect.y + yy * tile, Mathf.Min(tile, iconRect.xMax - (iconRect.x + xx * tile)), Mathf.Min(tile, iconRect.yMax - (iconRect.y + yy * tile)));
                            bool dark = ((xx + yy) & 1) == 0;
                            Color c = dark ? new Color(0.75f, 0.75f, 0.75f, 1f) : new Color(0.9f, 0.9f, 0.9f, 1f);
                            EditorGUI.DrawRect(t, c);
                        }
                    }
                }

                if (previewTex != null)
                {
                    // If UV is full rect, use EditorGUI.DrawPreviewTexture which is better suited for
                    // editor preview textures and render textures. Otherwise use DrawTextureWithTexCoords.
                    if (Mathf.Approximately(uv.x, 0f) && Mathf.Approximately(uv.y, 0f) && Mathf.Approximately(uv.width, 1f) && Mathf.Approximately(uv.height, 1f))
                    {
                        // Use GUI.DrawTexture with alphaBlend=true to ensure transparency composites over the
                        // background we drew. EditorGUI.DrawPreviewTexture sometimes renders visual previews
                        // in a way that results in a black background for certain preview textures, so prefer
                        // GUI.DrawTexture for consistent compositing.
                        if (EnableDebug) Debug.Log($"[PreviewObjectDrawer] Using GUI.DrawTexture for {obj}");
                        GUI.DrawTexture(iconRect, previewTex, ScaleMode.ScaleToFit, true);
                    }
                    else
                    {
                        if (EnableDebug) Debug.Log($"[PreviewObjectDrawer] Using DrawTextureWithTexCoords for {obj} uv={uv}");
                        // Draw only the sprite sub-rectangle (correctly handles atlas-packed sprites)
                        GUI.DrawTextureWithTexCoords(iconRect, previewTex, uv, true);
                    }
                }
                else
                {
                    // No preview available (yet). Draw a subtle placeholder so it's visible
                    // that something belongs here, instead of leaving empty space.
                    Color oldColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    GUI.Box(iconRect, GUIContent.none);
                    GUI.color = oldColor;
                }
            }

            if (labelRect.height > 0f && labelRect.width > 0f)
            {
                GUI.Label(labelRect, content.text ?? "None");
            }
        }

        // Resolve a texture and optional UVs (0..1) for drawing. This centralizes support for different Unity types
        // so we can correctly draw sprites (using only the sprite rect) and UI components (Image/RawImage).
        private static Texture ResolvePreviewTextureWithUV(Object obj, out Rect uv)
        {
            uv = new Rect(0f, 0f, 1f, 1f);
            if (obj == null) return null;

            int id = obj.GetInstanceID();
            if (PreviewCache.TryGetValue(id, out Texture cached) && cached != null)
            {
                // If we have a cached UV for this entry, return it as well
                if (PreviewUVCache.TryGetValue(id, out Rect cachedUV))
                {
                    uv = cachedUV;
                }
                return cached;
            }

            // 1) Immediate type-specific fast paths for texture-like objects and UI components.
            // These avoid waiting on AssetPreview and commonly solve the "texture exists but doesn't render" cases.
            // - Sprite: return its atlas texture and a UV rect corresponding to the sprite's region
            // - Texture2D / RenderTexture: return directly
            // - Unity UI Image / RawImage: return their underlying sprite/texture when possible

            // Sprite
            if (obj is Sprite sprite)
            {
                // First, try Unity's generated preview for the sprite. Unity will render the sprite correctly
                // (including packed/rotated atlases) if it has generated an AssetPreview.
                var generated = AssetPreview.GetAssetPreview(sprite);
                if (generated != null)
                {
                    PreviewCache[id] = generated;
                    PreviewUVCache[id] = new Rect(0f, 0f, 1f, 1f);
                    return generated;
                }
                Texture2D tex = sprite.texture;
                if (tex != null)
                {
                    // Calculate normalized UV rect for the sprite inside the texture.
                    Rect tr = sprite.textureRect;
                    float uvx = tr.x / tex.width;
                    float uvy = tr.y / tex.height;
                    float uvw = tr.width / tex.width;
                    float uvh = tr.height / tex.height;
                    uv = new Rect(uvx, uvy, uvw, uvh);

                    if (EnableDebug) Debug.Log($"[PreviewObjectDrawer] Sprite.textureRect={tr} texSize=({tex.width}x{tex.height}) uv={uv}");

                    PreviewCache[id] = tex;
                    PreviewUVCache[id] = uv;
                    return tex;
                }
            }

            // Texture2D
            if (obj is Texture2D t2d)
            {
                PreviewCache[id] = t2d;
                PreviewUVCache[id] = uv;
                return t2d;
            }

            // RenderTexture
            if (obj is RenderTexture rt)
            {
                PreviewCache[id] = rt;
                PreviewUVCache[id] = uv;
                return rt;
            }

            // Unity UI components: Image and RawImage (optional, using types from UnityEngine.UI)
            // Use as-cast to avoid hard dependency when UI assembly isn't present at compile time.
            var asComponent = obj as Component;
            if (asComponent != null)
            {
                // Image (has sprite)
                var imageType = typeof(UnityEngine.UI.Image);
                if (imageType.IsAssignableFrom(asComponent.GetType()))
                {
                    var img = asComponent as UnityEngine.UI.Image;
                    if (img != null)
                    {
                        if (img.sprite != null)
                        {
                            Texture2D tex = img.sprite.texture;
                            if (tex != null)
                            {
                                Rect tr = img.sprite.textureRect;
                                float uvx = tr.x / tex.width;
                                float uvy = tr.y / tex.height;
                                float uvw = tr.width / tex.width;
                                float uvh = tr.height / tex.height;
                                uv = new Rect(uvx, uvy, uvw, uvh);

                                PreviewCache[id] = tex;
                                PreviewUVCache[id] = uv;
                                return tex;
                            }
                        }

                        // If no sprite, try mainTexture (rare)
                        if (img.mainTexture != null)
                        {
                            PreviewCache[id] = img.mainTexture;
                            PreviewUVCache[id] = uv;
                            return img.mainTexture;
                        }
                    }
                }

                // RawImage
                var rawImageType = typeof(UnityEngine.UI.RawImage);
                if (rawImageType.IsAssignableFrom(asComponent.GetType()))
                {
                    var raw = asComponent as UnityEngine.UI.RawImage;
                    if (raw != null && raw.texture != null)
                    {
                        PreviewCache[id] = raw.texture;
                        PreviewUVCache[id] = uv;
                        return raw.texture;
                    }
                }

                // Material (if a Material component is passed, handle its mainTexture)
                if (asComponent is Renderer rend)
                {
                    // If a renderer instance is passed, try its sharedMaterial
                    if (rend.sharedMaterial != null && rend.sharedMaterial.mainTexture != null)
                    {
                        PreviewCache[id] = rend.sharedMaterial.mainTexture;
                        PreviewUVCache[id] = uv;
                        return rend.sharedMaterial.mainTexture;
                    }
                }
            }

            // 2) Try Unity's generated preview (may be async)
            Texture texPreview = AssetPreview.GetAssetPreview(obj);
            if (texPreview == null)
            {
                texPreview = AssetPreview.GetMiniThumbnail(obj);
            }

            if (texPreview != null)
            {
                PreviewCache[id] = texPreview;
                PreviewUVCache[id] = uv;
                return texPreview;
            }

            // 3) Try some further type-specific fallbacks (objects that aren't components)
            if (obj is Material mat && mat.mainTexture != null)
            {
                PreviewCache[id] = mat.mainTexture;
                PreviewUVCache[id] = uv;
                return mat.mainTexture;
            }

            if (obj is GameObject go)
            {
                // Attempt to get a thumbnail of the prefab/gameobject via AssetPreview (already tried), but also
                // try to find a Renderer and use its material texture as a last resort.
                var rend = go.GetComponentInChildren<Renderer>();
                if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.mainTexture != null)
                {
                    PreviewCache[id] = rend.sharedMaterial.mainTexture;
                    PreviewUVCache[id] = uv;
                    return rend.sharedMaterial.mainTexture;
                }
            }

            // Nothing found
            return null;
        }

        /// <summary>
        /// Clears the internal preview cache. Optional utility if you need to force-refresh
        /// previews after large asset changes.
        /// </summary>
        public static void ClearCache()
        {
            PreviewCache.Clear();
            PreviewUVCache.Clear();
        }
    }
}