using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace _VuTH.Common.DI.Editor
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
            // Only the container's own scene: references to objects in other scenes cannot be serialized and would be
            // lost on save. Sorted by hierarchy order so the list (and configuration order) is stable.
            var owner = ((Component)target).gameObject;
            var configuratorsInScene = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .AsValueEnumerable()
                .Where(m => m is IVContainerConfigurator && m.gameObject != owner && m.gameObject.scene == owner.scene)
                .OrderBy(m => m.transform.root.GetSiblingIndex())
                .ThenBy(m => m.transform.GetSiblingIndex())
                .ToList();

            if (configuratorsInScene.Count == 0)
            {
                Debug.LogWarning($"[SceneScopeContainer] No MonoBehaviours with IVContainerConfigurator found in scene '{owner.scene.name}'.");
                return;
            }

            Undo.RecordObject(target, "Fetch Scene Configurators");

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