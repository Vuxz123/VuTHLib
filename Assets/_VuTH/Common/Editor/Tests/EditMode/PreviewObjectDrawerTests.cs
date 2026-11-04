using _VuTH.Common.Editor.UI.PreviewObject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace _VuTH.Common.Editor.Tests.EditMode
{
    public class PreviewObjectDrawerTests
    {
        private Texture2D _texture;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(64, 32);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_texture);
        }

        private Sprite CreateSprite(Rect rect) => Sprite.Create(_texture, rect, Vector2.one * 0.5f);

        [Test]
        public void Texture_IsDrawnDirectly_WithFullUv()
        {
            var source = PreviewObjectDrawer.Resolve(_texture);

            Assert.AreSame(_texture, source.Texture);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), source.Uv);
            Assert.IsFalse(source.IsPending);
        }

        [Test]
        public void Sprite_UsesItsSubRectOfTheTexture()
        {
            var sprite = CreateSprite(new Rect(16f, 8f, 32f, 16f));

            var source = PreviewObjectDrawer.Resolve(sprite);

            Assert.AreSame(_texture, source.Texture);
            Assert.AreEqual(new Rect(0.25f, 0.25f, 0.5f, 0.5f), source.Uv);
            Object.DestroyImmediate(sprite);
        }

        [Test]
        public void Image_FollowsItsCurrentSprite()
        {
            _go = new GameObject("Image", typeof(RectTransform));
            var image = _go.AddComponent<Image>();
            var left = CreateSprite(new Rect(0f, 0f, 32f, 32f));
            var right = CreateSprite(new Rect(32f, 0f, 32f, 32f));

            image.sprite = left;
            Assert.AreEqual(0f, PreviewObjectDrawer.Resolve(image).Uv.x);

            image.sprite = right;
            Assert.AreEqual(0.5f, PreviewObjectDrawer.Resolve(image).Uv.x, "not stuck on the first sprite");

            Object.DestroyImmediate(left);
            Object.DestroyImmediate(right);
        }

        [Test]
        public void SceneGameObject_ResolvesThroughItsUiComponent()
        {
            _go = new GameObject("RawImage", typeof(RectTransform));
            var raw = _go.AddComponent<RawImage>();
            raw.texture = _texture;
            raw.uvRect = new Rect(0f, 0f, 0.5f, 1f);

            var source = PreviewObjectDrawer.Resolve(_go);

            Assert.AreSame(_texture, source.Texture);
            Assert.AreEqual(raw.uvRect, source.Uv);
        }

        [Test]
        public void NullOrDestroyed_ResolvesToNothing()
        {
            Assert.IsNull(PreviewObjectDrawer.Resolve(null).Texture);

            var destroyed = new Texture2D(1, 1);
            Object.DestroyImmediate(destroyed);
            Assert.IsNull(PreviewObjectDrawer.Resolve(destroyed).Texture);
        }

        [Test]
        public void FitToAspect_KeepsRatio_AndCenters()
        {
            var area = new Rect(0f, 0f, 100f, 100f);

            Assert.AreEqual(new Rect(0f, 25f, 100f, 50f), PreviewObjectDrawer.FitToAspect(area, 64f, 32f));
            Assert.AreEqual(new Rect(25f, 0f, 50f, 100f), PreviewObjectDrawer.FitToAspect(area, 32f, 64f));
            Assert.AreEqual(area, PreviewObjectDrawer.FitToAspect(area, 0f, 10f), "degenerate content keeps the area");
        }

        private static bool Inside(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 0.01f && inner.yMin >= outer.yMin - 0.01f &&
            inner.xMax <= outer.xMax + 0.01f && inner.yMax <= outer.yMax + 0.01f;

        [Test]
        public void Layout_SingleLine_IsInline_IconThenName()
        {
            var rect = new Rect(10f, 20f, 200f, 18f);
            var layout = PreviewObjectDrawer.CalculateLayout(rect);

            Assert.AreEqual(PreviewObjectDrawer.PreviewLayoutMode.Inline, layout.Mode);
            Assert.IsTrue(Inside(rect, layout.Thumbnail));
            Assert.LessOrEqual(layout.Thumbnail.width, 16f);
            Assert.Greater(layout.Title.xMin, layout.Thumbnail.xMax, "name right of the icon");
            Assert.IsTrue(Inside(rect, layout.Title));
        }

        [Test]
        public void Layout_Wide_IsRow_SquareThumbnailThenNameAndType()
        {
            var rect = new Rect(0f, 0f, 240f, 48f);
            var layout = PreviewObjectDrawer.CalculateLayout(rect);

            Assert.AreEqual(PreviewObjectDrawer.PreviewLayoutMode.Row, layout.Mode);
            Assert.AreEqual(layout.Thumbnail.width, layout.Thumbnail.height, "square thumbnail");
            Assert.Greater(layout.Title.xMin, layout.Thumbnail.xMax);
            Assert.Greater(layout.Subtitle.width, 0f, "type shown under the name");
            Assert.AreEqual(layout.Title.yMax, layout.Subtitle.yMin);
            Assert.IsTrue(Inside(rect, layout.Subtitle));
        }

        [Test]
        public void Layout_Square_IsCard_WithCaptionBelow()
        {
            // Used to give the name no room at all for square rects.
            var rect = new Rect(0f, 0f, 80f, 80f);
            var layout = PreviewObjectDrawer.CalculateLayout(rect);

            Assert.AreEqual(PreviewObjectDrawer.PreviewLayoutMode.Card, layout.Mode);
            Assert.Greater(layout.Title.width, 0f, "caption visible");
            Assert.GreaterOrEqual(layout.Title.yMin, layout.Thumbnail.yMax, "caption below the thumbnail");
            Assert.IsTrue(Inside(rect, layout.Title));
            Assert.IsTrue(Inside(rect, layout.Thumbnail));
        }

        [Test]
        public void Layout_TooSmallForText_KeepsOnlyThePicture()
        {
            var narrowCard = PreviewObjectDrawer.CalculateLayout(new Rect(0f, 0f, 30f, 40f));
            Assert.AreEqual(Rect.zero, narrowCard.Title);
            Assert.Greater(narrowCard.Thumbnail.width, 0f);

            var tinyInline = PreviewObjectDrawer.CalculateLayout(new Rect(0f, 0f, 30f, 18f));
            Assert.AreEqual(Rect.zero, tinyInline.Title);
        }

        [Test]
        public void SpriteUv_IsNormalized_AndSafeForEmptyTextures()
        {
            Assert.AreEqual(new Rect(0.5f, 0f, 0.5f, 0.5f),
                PreviewObjectDrawer.SpriteUv(new Rect(32f, 0f, 32f, 16f), 64f, 32f));
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f),
                PreviewObjectDrawer.SpriteUv(new Rect(0f, 0f, 1f, 1f), 0f, 0f));
        }
    }
}
