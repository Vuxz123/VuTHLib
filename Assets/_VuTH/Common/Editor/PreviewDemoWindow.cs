using _VuTH.Common.Editor.UI.PreviewObject;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor
{
    public class PreviewDemoWindow : EditorWindow
    {
        private Sprite _sprite;
        private Texture2D _texture2D;
        private RenderTexture _renderTexture;
        private Material _material;
        private GameObject _gameObjectAsset;
        private GameObject _uiGameObject;

        [MenuItem("Window/VuTH/Preview Demo")]
        public static void ShowWindow()
        {
            var w = GetWindow<PreviewDemoWindow>("Preview Demo");
            w.minSize = new Vector2(300, 300);
        }

        private void OnEnable()
        {
            PreviewObjectDrawer.EnableDebug = true;
        }

        private void OnDisable()
        {
            PreviewObjectDrawer.EnableDebug = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Preview Demo for multiple Unity types", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Sprite", EditorStyles.boldLabel);
            _sprite = (Sprite)EditorGUILayout.ObjectField(_sprite, typeof(Sprite), false);
            DrawPreviewForObject(_sprite);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Texture2D", EditorStyles.boldLabel);
            _texture2D = (Texture2D)EditorGUILayout.ObjectField(_texture2D, typeof(Texture2D), false);
            DrawPreviewForObject(_texture2D);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("RenderTexture", EditorStyles.boldLabel);
            _renderTexture = (RenderTexture)EditorGUILayout.ObjectField(_renderTexture, typeof(RenderTexture), false);
            DrawPreviewForObject(_renderTexture);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Material", EditorStyles.boldLabel);
            _material = (Material)EditorGUILayout.ObjectField(_material, typeof(Material), false);
            DrawPreviewForObject(_material);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("GameObject (prefab/scene) - will use Renderer.mainTexture if available", EditorStyles.boldLabel);
            _gameObjectAsset = (GameObject)EditorGUILayout.ObjectField(_gameObjectAsset, typeof(GameObject), true);
            DrawPreviewForObject(_gameObjectAsset);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("UI GameObject (Image/RawImage)", EditorStyles.boldLabel);
            _uiGameObject = (GameObject)EditorGUILayout.ObjectField(_uiGameObject, typeof(GameObject), true);
            DrawPreviewForObject(_uiGameObject);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            if (GUILayout.Button("Clear Preview Cache"))
            {
                PreviewObjectDrawer.ClearCache();
                Repaint();
            }

            EditorGUILayout.HelpBox("If a preview doesn't appear immediately, wait a moment — AssetPreview is asynchronous. Texture-like types (Sprite/Texture2D/RenderTexture/Material) are drawn immediately when possible.", MessageType.Info);
        }

        private void DrawPreviewForObject(Object obj)
        {
            Rect r = GUILayoutUtility.GetRect(80, 80, GUILayout.ExpandWidth(true));
            // Make a square preview area
            float size = Mathf.Min(r.width, 80f);
            Rect sq = new Rect(r.x + (r.width - size) * 0.5f, r.y, size, size);
            PreviewObjectDrawer.DrawObjectPreview(sq, obj);
        }
    }
}
