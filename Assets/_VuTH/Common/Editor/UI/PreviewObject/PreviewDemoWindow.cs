using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace _VuTH.Common.Editor.UI.PreviewObject
{
    /// <summary>Editor window to try <see cref="PreviewObjectDrawer"/> on the supported object types.</summary>
    public class PreviewDemoWindow : EditorWindow
    {
        private const float PreviewSize = 80f;

        private Sprite _sprite;
        private Texture2D _texture2D;
        private RenderTexture _renderTexture;
        private Material _material;
        private GameObject _gameObject;
        private Image _image;
        private RawImage _rawImage;
        private Vector2 _scroll;

        [MenuItem("Window/VuTH/Preview Demo")]
        public static void ShowWindow()
        {
            var window = GetWindow<PreviewDemoWindow>("Preview Demo");
            window.minSize = new Vector2(300f, 300f);
        }

        private void OnGUI()
        {
            PreviewObjectDrawer.ShowCheckerboard =
                EditorGUILayout.Toggle("Show Checkerboard", PreviewObjectDrawer.ShowCheckerboard);
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _sprite = DrawSection("Sprite", _sprite, false);
            _texture2D = DrawSection("Texture2D", _texture2D, false);
            _renderTexture = DrawSection("RenderTexture", _renderTexture, false);
            _material = DrawSection("Material", _material, false);
            _gameObject = DrawSection("GameObject (prefab or scene: Image / RawImage / SpriteRenderer / Renderer)", _gameObject, true);
            _image = DrawSection("UI Image", _image, true);
            _rawImage = DrawSection("UI RawImage", _rawImage, true);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "Prefab / mesh / material previews are generated asynchronously by Unity: the type icon is shown " +
                "until they are ready, then the window repaints. Textures and sprites are drawn immediately.",
                MessageType.Info);
        }

        private static T DrawSection<T>(string title, T value, bool allowSceneObjects) where T : Object
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            value = (T)EditorGUILayout.ObjectField(value, typeof(T), allowSceneObjects);

            var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(true));
            var size = Mathf.Min(rect.width, PreviewSize);
            PreviewObjectDrawer.DrawObjectPreview(new Rect(rect.x + (rect.width - size) * 0.5f, rect.y, size, size), value);

            EditorGUILayout.EndVertical();
            return value;
        }
    }
}
