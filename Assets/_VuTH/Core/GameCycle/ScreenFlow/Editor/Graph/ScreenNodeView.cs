using System;
using System.Collections.Generic;
using _VuTH.Core.GameCycle.Screen;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Core.A;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    public sealed class ScreenNodeView : Node
    {
        private const string BadgeName = "screenflow-badge";

        private readonly Label _screenIdLabel;
        private readonly Label _assetLabel;
        private readonly ObjectField _screenField;

        public string Guid { get; }
        public ScreenModel Screen { get; private set; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        private Action<string> _onSetStartNode;
        private Action<ScreenModel> _onSetScreen;
        private Action<string> _onRemoveNode;
        private Action<string> _onPingScreen;

        public ScreenNodeView(
            string guid,
            Action<string> onSetStartNode,
            Action<ScreenModel> onSetScreen,
            Action<string> onRemoveNode,
            Action<string> onPingScreen)
        {
            Guid = guid;
            _onSetStartNode = onSetStartNode;
            _onSetScreen = onSetScreen;
            _onRemoveNode = onRemoveNode;
            _onPingScreen = onPingScreen;

            _screenField = new ObjectField("Screen")
            {
                objectType = typeof(ScreenModel),
                allowSceneObjects = false
            };
            _screenField.RegisterValueChangedCallback(evt => onSetScreen?.Invoke(evt.newValue as ScreenModel));

            _screenIdLabel = new Label();
            _assetLabel = new Label();

            extensionContainer.style.paddingLeft = 4;
            extensionContainer.style.paddingRight = 4;
            extensionContainer.Add(_screenField);
            extensionContainer.Add(_screenIdLabel);
            extensionContainer.Add(_assetLabel);

            InputPort = CreatePort(Direction.Input, "In");
            inputContainer.Add(InputPort);

            OutputPort = CreatePort(Direction.Output, "Out");
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 1) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Set as Start Node"), false, () => _onSetStartNode?.Invoke(Guid));
            menu.AddItem(new GUIContent("Ping ScreenModel"), false, () => _onPingScreen?.Invoke(Guid));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Remove Node"), false, () => _onRemoveNode?.Invoke(Guid));
            menu.ShowAsContext();
            evt.StopPropagation();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (!HasDraggedScreenModel()) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var screens = GetDraggedScreenModels();
            if (screens.Count == 0) return;

            DragAndDrop.AcceptDrag();
            _onSetScreen?.Invoke(screens[0]);
            evt.StopPropagation();
        }

        public void Bind(ScreenFlowNode node)
        {
            Screen = node.Screen;
            title = node.Screen ? node.Screen.name : "<Missing Screen>";
            SetPosition(new Rect(node.EditorPosition, new Vector2(280f, 190f)));
            _screenField.SetValueWithoutNotify(node.Screen);
            _screenIdLabel.text = node.Screen && node.Screen.ScreenID
                ? $"ScreenId: {node.Screen.ScreenID}"
                : "ScreenId: <None>";
            _assetLabel.text = node.Screen
                ? $"Asset: {node.Screen.name}"
                : "Asset: <Missing ScreenModel>";
        }

        public void ApplyState(bool isStartNode, bool isMissingScreen)
        {
            titleContainer.style.backgroundColor = StyleKeyword.Null;
            mainContainer.style.backgroundColor = StyleKeyword.Null;

            var badge = titleContainer.Q<Label>(BadgeName);
            badge?.RemoveFromHierarchy();

            if (isStartNode || isMissingScreen)
            {
                badge = new Label(isStartNode ? "Start" : "Missing")
                {
                    name = BadgeName
                };
                badge.style.marginLeft = 6;
                badge.style.unityTextAlign = TextAnchor.MiddleRight;
                badge.style.color = isStartNode
                    ? new Color(1f, 1f, 1f, 0.9f)
                    : new Color(1f, 0.9f, 0.4f, 1f);
                titleContainer.Add(badge);
            }

            if (isStartNode)
            {
                titleContainer.style.backgroundColor = new Color(0.15f, 0.55f, 0.25f, 0.55f);
            }

            if (isMissingScreen)
            {
                mainContainer.style.backgroundColor = new Color(0.65f, 0.15f, 0.15f, 0.25f);
            }
        }

        private Port CreatePort(Direction direction, string portName)
        {
            var port = InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(bool));
            port.portName = portName;
            port.portColor = Color.white;
            return port;
        }

        private static bool HasDraggedScreenModel()
        {
            return GetDraggedScreenModels().Count > 0;
        }

        private static List<ScreenModel> GetDraggedScreenModels()
        {
            var screens = new List<ScreenModel>();
            var objectReferences = DragAndDrop.objectReferences;
            if (objectReferences == null) return screens;

            for (var i = 0; i < objectReferences.Length; i++)
            {
                if (objectReferences[i] is ScreenModel screen)
                {
                    screens.Add(screen);
                }
            }

            return screens;
        }
    }
}
