using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Common.DI.Editor
{
    [CustomEditor(typeof(SceneScopeContainer))]
    public class SceneScopeContainerInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            // Vẽ mặc định
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            root.Add(new VisualElement { style = { height = 10 } });

            var fetchButton = new Button(FetchConfiguratorsFromScene)
            {
                text = "Fetch Configurators from Current Scene",
                style =
                {
                    height = 30,
                    backgroundColor = new Color(0.1f, 0.5f, 0.8f),
                    color = Color.white,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            
            root.Add(fetchButton);

            return root;
        }

        private void FetchConfiguratorsFromScene()
        {
            // Tìm tất cả các MonoBehaviour thực thi interface
            var configuratorsInScene = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => m is IVContainerConfigurator && m.gameObject != ((Component)target).gameObject) 
                .ToList();

            if (configuratorsInScene.Count == 0)
            {
                Debug.LogWarning("[SceneScopeContainer] No MonoBehaviours with IVContainerConfigurator found.");
                return;
            }

            Undo.RecordObject(target, "Fetch Scene Configurators");

            // Trỏ vào đúng field sceneConfigurators (không phải field dùng SerializeReference)
            var sceneProp = serializedObject.FindProperty("configurators");
            
            sceneProp.ClearArray();
            for (int i = 0; i < configuratorsInScene.Count; i++)
            {
                sceneProp.InsertArrayElementAtIndex(i);
                // Với MonoBehaviour, ta dùng objectReferenceValue
                sceneProp.GetArrayElementAtIndex(i).objectReferenceValue = configuratorsInScene[i];
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"[SceneScopeContainer] Linked {configuratorsInScene.Count} MonoBehaviours from scene.");
        }
    }
}