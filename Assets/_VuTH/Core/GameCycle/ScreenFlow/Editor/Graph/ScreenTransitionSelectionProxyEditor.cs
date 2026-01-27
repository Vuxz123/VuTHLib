using _VuTH.Core.GameCycle.ScreenFlow.Condition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    [CustomEditor(typeof(ScreenTransitionSelectionProxy))]
    internal sealed class ScreenTransitionSelectionProxyEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column
                }
            };

            var fromProp = serializedObject.FindProperty("fromGuid");
            var toProp = serializedObject.FindProperty("toGuid");
            var eventProp = serializedObject.FindProperty("eventName");
            var conditionProp = serializedObject.FindProperty("condition");

            var fromField = new TextField("From") { isReadOnly = true };
            fromField.SetValueWithoutNotify(fromProp.stringValue);
            fromField.SetEnabled(false);

            var toField = new TextField("To") { isReadOnly = true };
            toField.SetValueWithoutNotify(toProp.stringValue);
            toField.SetEnabled(false);

            var eventField = new TextField("Event Name") { value = eventProp.stringValue };

            var conditionField = new ObjectField("Condition")
            {
                objectType = typeof(TransitionCondition),
                allowSceneObjects = false,
                value = conditionProp.objectReferenceValue as TransitionCondition
            };

            eventField.RegisterValueChangedCallback(_ => Apply());
            conditionField.RegisterValueChangedCallback(_ => Apply());

            var buttonsRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            var createBtn = new Button(() =>
            {
                var created = CreateConditionAsset<AlwaysTrueCondition>();
                if (!created)
                    return;

                conditionField.SetValueWithoutNotify(created);
                Apply();
            })
            {
                text = "Create AlwaysTrueCondition",
                style =
                {
                    marginRight = 6
                }
            };

            var pingBtn = new Button(() =>
            {
                if (!conditionField.value)
                    return;
                EditorGUIUtility.PingObject(conditionField.value);
            })
            {
                text = "Ping Condition"
            };

            buttonsRow.Add(createBtn);
            buttonsRow.Add(pingBtn);

            var helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);

            eventField.RegisterValueChangedCallback(_ => RefreshHelp());
            conditionField.RegisterValueChangedCallback(_ => RefreshHelp());
            RefreshHelp();

            root.Add(fromField);
            root.Add(toField);

            root.Add(new VisualElement { style = { height = 6 } });
            root.Add(eventField);
            root.Add(conditionField);

            root.Add(new VisualElement { style = { height = 8 } });
            root.Add(buttonsRow);

            root.Add(new VisualElement { style = { height = 8 } });
            root.Add(helpBox);

            return root;

            void Apply()
            {
                serializedObject.Update();
                eventProp.stringValue = eventField.value;
                conditionProp.objectReferenceValue = conditionField.value;
                serializedObject.ApplyModifiedProperties();

                ((ScreenTransitionSelectionProxy)target).ApplyToGraph();
            }

            void RefreshHelp()
            {
                if (string.IsNullOrWhiteSpace(eventField.value))
                {
                    helpBox.messageType = HelpBoxMessageType.Warning;
                    helpBox.text = "Transition is missing Event Name.";
                    return;
                }

                if (conditionField.value == null)
                {
                    helpBox.messageType = HelpBoxMessageType.Info;
                    helpBox.text = "No condition: transition will always be allowed.";
                    return;
                }

                helpBox.messageType = HelpBoxMessageType.None;
                helpBox.text = string.Empty;
            }
        }

        private static T CreateConditionAsset<T>() where T : TransitionCondition
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Condition Asset",
                typeof(T).Name,
                "asset",
                "Choose location for the condition asset",
                "Assets");

            if (string.IsNullOrEmpty(path))
                return null;

            var asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }
    }
}
