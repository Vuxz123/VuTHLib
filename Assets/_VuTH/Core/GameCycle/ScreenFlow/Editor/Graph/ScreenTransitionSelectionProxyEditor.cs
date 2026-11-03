using System;
using _VuTH.Core.GameCycle.ScreenFlow.Condition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
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
            var navigationProp = serializedObject.FindProperty("navigation");
            var screenBelowProp = serializedObject.FindProperty("screenBelow");

            var fromField = new TextField("From") { isReadOnly = true };
            fromField.SetValueWithoutNotify(fromProp.stringValue);
            fromField.SetEnabled(false);

            var toField = new TextField("To") { isReadOnly = true };
            toField.SetValueWithoutNotify(toProp.stringValue);
            toField.SetEnabled(false);

            var eventField = new TextField("Event Name") { value = eventProp.stringValue };
            eventField.style.flexGrow = 1;

            // Pick an event name already used in this graph (avoids typos / near-duplicates).
            var eventRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var pickEventBtn = new Button(() =>
            {
                var graph = ((ScreenTransitionSelectionProxy)target).Graph;
                var menu = new GenericMenu();
                var names = Codegen.ScreenFlowEventsGenerator.CollectEvents(graph).Keys;
                foreach (var name in names)
                {
                    var captured = name;
                    menu.AddItem(new GUIContent(name), name == eventField.value, () => eventField.value = captured);
                }
                if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No events in this graph yet"));
                menu.ShowAsContext();
            })
            {
                text = "▾",
                tooltip = "Choose an event name already used in this graph"
            };
            eventRow.Add(eventField);
            eventRow.Add(pickEventBtn);

            var navigationField = new EnumField("Navigation", (ScreenFlowNavigation)navigationProp.intValue)
            {
                tooltip = "How the ScreenManager is driven when this transition fires."
            };
            var screenBelowField = new EnumField("Screen Below", (ScreenBelowVisibility)screenBelowProp.intValue)
            {
                tooltip = "Push only: hide the current screen while the target is on top (ScreenDefault = target's hideScreenBelow)."
            };

            var conditionField = new ObjectField("Condition")
            {
                objectType = typeof(TransitionCondition),
                allowSceneObjects = false,
                value = conditionProp.objectReferenceValue as TransitionCondition
            };

            eventField.RegisterValueChangedCallback(_ => Apply());
            conditionField.RegisterValueChangedCallback(_ => Apply());
            navigationField.RegisterValueChangedCallback(_ => { Apply(); RefreshNavigation(); });
            screenBelowField.RegisterValueChangedCallback(_ => Apply());

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
            RefreshNavigation();

            root.Add(fromField);
            root.Add(toField);

            root.Add(new VisualElement { style = { height = 6 } });
            root.Add(eventRow);
            root.Add(conditionField);
            root.Add(navigationField);
            root.Add(screenBelowField);

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
                navigationProp.intValue = Convert.ToInt32(navigationField.value);
                screenBelowProp.intValue = Convert.ToInt32(screenBelowField.value);
                serializedObject.ApplyModifiedProperties();

                ((ScreenTransitionSelectionProxy)target).ApplyToGraph();
            }

            void RefreshNavigation()
            {
                var mode = (ScreenFlowNavigation)navigationField.value;
                screenBelowField.style.display = mode == ScreenFlowNavigation.Push ? DisplayStyle.Flex : DisplayStyle.None;
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
