using _VuTH.Core.GameCycle.Screen;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    [CustomEditor(typeof(ScreenNodeSelectionProxy))]
    internal sealed class ScreenNodeSelectionProxyEditor : UnityEditor.Editor
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

            var guidProp = serializedObject.FindProperty("nodeGuid");
            var graphProp = serializedObject.FindProperty("graph");
            var screenProp = serializedObject.FindProperty("screen");

            var guidField = new TextField("Guid") { isReadOnly = true };
            guidField.SetValueWithoutNotify(guidProp.stringValue);
            guidField.SetEnabled(false);

            var startField = new Toggle("Start Node") { value = false };
            startField.SetEnabled(false);
            if (graphProp.objectReferenceValue is ScreenFlowGraph g)
                startField.SetValueWithoutNotify(g.StartNodeGuid == guidProp.stringValue);

            var screenField = new ObjectField("ScreenModel")
            {
                objectType = typeof(ScreenModel),
                allowSceneObjects = false,
                value = screenProp.objectReferenceValue as ScreenModel
            };

            screenField.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                screenProp.objectReferenceValue = evt.newValue as ScreenModel;
                serializedObject.ApplyModifiedProperties();
            });

            var buttonsRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            var proxy = (ScreenNodeSelectionProxy)target;

            var pingBtn = new Button(proxy.PingScreen)
            {
                text = "Ping ScreenModel",
                style =
                {
                    marginRight = 6
                }
            };

            var startBtn = new Button(proxy.SetAsStartNode) { text = "Set as Start" };

            buttonsRow.Add(pingBtn);
            buttonsRow.Add(startBtn);

            var helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);

            screenField.RegisterValueChangedCallback(_ => RefreshHelp());
            RefreshHelp();

            root.Add(guidField);
            root.Add(startField);
            root.Add(new VisualElement { style = { height = 6 } });
            root.Add(screenField);
            root.Add(new VisualElement { style = { height = 8 } });
            root.Add(buttonsRow);
            root.Add(new VisualElement { style = { height = 8 } });
            root.Add(helpBox);

            return root;

            void RefreshHelp()
            {
                if (proxy.Screen)
                {
                    helpBox.messageType = HelpBoxMessageType.Info;
                    helpBox.text = $"ScreenId: {proxy.Screen.ScreenID}";
                }
                else
                {
                    helpBox.messageType = HelpBoxMessageType.Warning;
                    helpBox.text = "Missing ScreenModel reference.";
                }
            }
        }
    }
}
