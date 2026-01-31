using System;
using System.Collections.Generic;
using System.Linq;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    public class ScreenFlowGraphView : GraphView, IDisposable
    {
        #region Fields & Constants

        private const string EdgeBoundViewDataKey = "__sf_edge_bound__";
        
        private readonly ScreenFlowGraphEditorWindow _window;
        private readonly Dictionary<string, ScreenNodeView> _nodeViewsByGuid = new();
        
        private ScreenFlowGraph _graph;
        private bool _isRebuildingView;

        #endregion

        #region Constructor & Dispose

        public ScreenFlowGraphView(ScreenFlowGraphEditorWindow window)
        {
            _window = window;

            style.flexGrow = 1;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;

            AddElement(CreateEntryPointHint());

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        public void Dispose()
        {
            // Clean up resources if needed
        }

        #endregion

        #region Public API

        public void PopulateView(ScreenFlowGraph graph)
        {
            _graph = graph;
            _isRebuildingView = true;
            
            try
            {
                // Clear existing view elements
                var elements = graphElements.ToList(); // ToList prevents modification during iteration
                DeleteElements(elements);
                _nodeViewsByGuid.Clear();

                if (!graph) return;

                // Create Nodes
                foreach (var node in graph.Nodes)
                {
                    var nodeView = CreateNodeView(node);
                    AddElement(nodeView);
                }

                // Create Edges
                foreach (var transition in graph.Transitions)
                {
                    if (!_nodeViewsByGuid.TryGetValue(transition.FromNodeGuid, out var fromNode)) continue;
                    if (!_nodeViewsByGuid.TryGetValue(transition.ToNodeGuid, out var toNode)) continue;

                    var edge = fromNode.OutputPort.ConnectTo(toNode.InputPort);
                    edge.userData = transition;
                    edge.Add(new ScreenTransitionLabel(transition));

                    BindCallbackOnEdge(edge, OnEdgeMouseDown);
                    AddElement(edge);
                }

                FrameAll();
            }
            finally
            {
                _isRebuildingView = false;
            }
        }

        #endregion

        #region GraphView Overrides

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            evt.menu.AppendAction("Add Screen Node", action => AddScreenNodeAt(action.eventInfo.mousePosition), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Validate Graph", _ => ValidateGraph(), DropdownMenuAction.AlwaysEnabled);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                if (port == startPort) return;
                if (port.node == startPort.node) return;
                if (port.direction == startPort.direction) return;
                
                compatible.Add(port);
            });
            return compatible;
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);

            if (_graph == null)
            {
                _window.ShowSelectionInspector(null);
                return;
            }

            // Handle Edge Selection
            if (selectable is Edge { userData: ScreenFlowTransition transition })
            {
                _window.ShowSelectionInspector(ScreenTransitionSelectionProxy.Create(_graph, transition));
                return;
            }

            // Handle Node Selection
            if (selectable is ScreenNodeView nodeView)
            {
                var nodeData = _graph.Nodes.AsValueEnumerable().FirstOrDefault(n => n.Guid == nodeView.Guid);
                if (nodeData != null)
                {
                    _window.ShowSelectionInspector(ScreenNodeSelectionProxy.Create(_graph, nodeData));
                    return;
                }
            }

            _window.ShowSelectionInspector(null);
        }

        #endregion

        #region Graph Change Logic (Add/Remove/Move)

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_isRebuildingView || _graph == null)
                return change;

            // --- 1. Xử lý Remove ---
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    switch (element)
                    {
                        case Edge edge:
                            RemoveTransition(edge);
                            break;
                        case ScreenNodeView node:
                            RemoveNode(node.Guid);
                            break;
                    }
                }
            }

            // --- 2. Xử lý Create Edge ---
            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge.isGhostEdge)
                    {
                        this.LogWarning("Try to create transition on ghost edge! Aborted!");
                        continue;
                    }
                    CreateTransitionAndBind(edge);
                }
            }

            // --- 3. Xử lý Move Node ---
            if (change.movedElements != null)
            {
                foreach (var moved in change.movedElements)
                {
                    if (moved is ScreenNodeView nodeView) PersistNodePosition(nodeView);
                }
            }

            return change;
        }

        private void CreateTransitionAndBind(Edge edge)
        {
            this.Log("Create Transition");

            if (edge.output?.node is not ScreenNodeView fromNode || edge.input?.node is not ScreenNodeView toNode)
            {
                this.LogError("❌ Lỗi: Không xác định được Node đầu/cuối.");
                return;
            }

            try
            {
                Undo.RecordObject(_graph, "Add Transition");
                var so = new SerializedObject(_graph);
                so.Update();

                var transitionsProp = so.FindProperty("transitions");
                var newIndex = transitionsProp.arraySize;
                transitionsProp.arraySize++;

                var added = transitionsProp.GetArrayElementAtIndex(newIndex);
                added.FindPropertyRelative("fromNodeGuid").stringValue = fromNode.Guid;
                added.FindPropertyRelative("toNodeGuid").stringValue = toNode.Guid;
                added.FindPropertyRelative("eventName").stringValue = "";
                added.FindPropertyRelative("condition").objectReferenceValue = null;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_graph);

                // Lấy data vừa tạo
                var createdTransition = _graph.Transitions[newIndex];
                if (createdTransition == null)
                {
                    Debug.LogError("❌ Lỗi: Data vừa tạo bị null.");
                    return;
                }

                // Bind Data & Callback
                edge.userData = createdTransition;
                edge.Add(new ScreenTransitionLabel(createdTransition));
                edge.viewDataKey = string.Empty; // Reset key to ensure callback registration

                ClearSelection();
                AddToSelection(edge);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Exception khi tạo Edge: {e.Message}\n{e.StackTrace}");
            }
        }

        private void RemoveTransition(Edge edge)
        {
            if (_isRebuildingView || !_graph) return;

            string fromGuid = null;
            string toGuid = null;

            if (edge.userData is ScreenFlowTransition transitionToRemove)
            {
                fromGuid = transitionToRemove.FromNodeGuid;
                toGuid = transitionToRemove.ToNodeGuid;
            }
            else
            {
                if (edge.output?.node is ScreenNodeView outputNode) fromGuid = outputNode.Guid;
                if (edge.input?.node is ScreenNodeView inputNode) toGuid = inputNode.Guid;

                Debug.LogWarning($"Cảnh báo: Xóa Edge userData null. Thử xóa theo Node ID: {fromGuid} -> {toGuid}");
            }

            if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid))
            {
                Debug.LogError("Không thể xóa Transition vì thiếu thông tin Node.");
                return;
            }

            Undo.RecordObject(_graph, "Remove Transition");
            var so = new SerializedObject(_graph);
            so.Update();
            
            var transitionsProp = so.FindProperty("transitions");
            var found = false;
            
            for (var i = 0; i < transitionsProp.arraySize; i++)
            {
                var t = transitionsProp.GetArrayElementAtIndex(i);
                if (t.FindPropertyRelative("fromNodeGuid").stringValue != fromGuid ||
                    t.FindPropertyRelative("toNodeGuid").stringValue != toGuid) continue;
                transitionsProp.DeleteArrayElementAtIndex(i);
                found = true;
                break;
            }

            if (found)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_graph);
            }
        }

        private void RemoveNode(string guid)
        {
            if (_isRebuildingView || !_graph) return;

            Undo.RecordObject(_graph, "Remove Screen Node");
            var so = new SerializedObject(_graph);
            so.Update();

            // Remove connected transitions
            var transitionsProp = so.FindProperty("transitions");
            for (var i = transitionsProp.arraySize - 1; i >= 0; i--)
            {
                var t = transitionsProp.GetArrayElementAtIndex(i);
                var from = t.FindPropertyRelative("fromNodeGuid").stringValue;
                var to = t.FindPropertyRelative("toNodeGuid").stringValue;
                if (from == guid || to == guid)
                {
                    transitionsProp.DeleteArrayElementAtIndex(i);
                }
            }

            // Remove node
            var nodesProp = so.FindProperty("nodes");
            for (var i = 0; i < nodesProp.arraySize; i++)
            {
                var n = nodesProp.GetArrayElementAtIndex(i);
                if (n.FindPropertyRelative("guid").stringValue != guid) continue;
                nodesProp.DeleteArrayElementAtIndex(i);
                break;
            }

            // Clear start node if matches
            if (so.FindProperty("startNodeGuid").stringValue == guid)
            {
                so.FindProperty("startNodeGuid").stringValue = string.Empty;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);

            EditorApplication.delayCall += () => { PopulateView(_graph); };
        }

        private void PersistNodePosition(ScreenNodeView nodeView)
        {
            if (!_graph) return;

            var pos = nodeView.GetPosition().position;

            Undo.RecordObject(_graph, "Move Screen Node");
            var so = new SerializedObject(_graph);
            so.Update();
            
            var nodesProp = so.FindProperty("nodes");
            for (var i = 0; i < nodesProp.arraySize; i++)
            {
                var n = nodesProp.GetArrayElementAtIndex(i);
                if (n.FindPropertyRelative("guid").stringValue != nodeView.Guid) continue;
                n.FindPropertyRelative("editorPosition").vector2Value = pos;
                break;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
        }

        #endregion

        #region Node Management

        private void AddScreenNodeAt(Vector2 mousePosition)
        {
            if (_graph == null) return;
            var localPos = contentViewContainer.WorldToLocal(mousePosition);
            AddScreenNodeAtPosition(localPos, null);
            EditorApplication.delayCall += () => { PopulateView(_graph); };
        }

        private void AddScreenNodeAtPosition(Vector2 position, ScreenModel screen)
        {
            if (_graph == null) return;

            // Prevent duplicate ScreenModels
            if (screen != null && _graph.Nodes.Any(n => n.Screen == screen))
                return;

            var newGuid = GUID.Generate().ToString();

            Undo.RecordObject(_graph, "Add Screen Node");
            var so = new SerializedObject(_graph);
            so.Update();
            
            var nodesProp = so.FindProperty("nodes");
            int newIndex = nodesProp.arraySize;
            nodesProp.arraySize++;

            var added = nodesProp.GetArrayElementAtIndex(newIndex);
            added.FindPropertyRelative("guid").stringValue = newGuid;
            added.FindPropertyRelative("screen").objectReferenceValue = screen;
            added.FindPropertyRelative("editorPosition").vector2Value = position;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
        }

        private ScreenNodeView CreateNodeView(ScreenFlowNode node)
        {
            var nodeView = new ScreenNodeView
            {
                Guid = node.Guid,
                Screen = node.Screen,
                title = node.Screen != null ? node.Screen.name : "<Missing Screen>",
            };

            nodeView.SetPosition(new Rect(node.EditorPosition, new Vector2(280, 200)));

            // Setup UI Elements (Screen Field)
            if (nodeView.ScreenField == null)
            {
                nodeView.ScreenField = new ObjectField("Screen")
                {
                    objectType = typeof(ScreenModel),
                    allowSceneObjects = false,
                };
                nodeView.ScreenField.RegisterValueChangedCallback(evt =>
                {
                    if (_graph != null) SetNodeScreen(nodeView.Guid, evt.newValue as ScreenModel);
                });

                nodeView.ScreenIdLabel = new Label();
                nodeView.AssetLabel = new Label();

                nodeView.extensionContainer.Clear();
                nodeView.extensionContainer.Add(nodeView.ScreenField);
                nodeView.extensionContainer.Add(nodeView.ScreenIdLabel);
                nodeView.extensionContainer.Add(nodeView.AssetLabel);
                nodeView.extensionContainer.style.paddingLeft = 4;
                nodeView.extensionContainer.style.paddingRight = 4;
            }

            UpdateNodeViewUI(nodeView, node.Screen);

            // Drag & Drop Handler
            nodeView.RegisterCallback<DragUpdatedEvent>(NodeDragUpdateCallback);
            nodeView.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!_graph) return;
                var refs = DragAndDrop.objectReferences;
                var dropped = refs?.OfType<ScreenModel>().FirstOrDefault();

                if (!dropped) return;
                DragAndDrop.AcceptDrag();
                SetNodeScreen(nodeView.Guid, dropped);
                evt.StopPropagation();
            });

            // Ports
            nodeView.InputPort = CreatePort(nodeView, Direction.Input, Port.Capacity.Multi);
            nodeView.InputPort.portName = "In";
            nodeView.inputContainer.Add(nodeView.InputPort);

            nodeView.OutputPort = CreatePort(nodeView, Direction.Output, Port.Capacity.Multi);
            nodeView.OutputPort.portName = "Out";
            nodeView.outputContainer.Add(nodeView.OutputPort);

            nodeView.RefreshExpandedState();
            nodeView.RefreshPorts();

            _nodeViewsByGuid[node.Guid] = nodeView;

            ApplyNodeStyling(nodeView);

            // Context Menu (Right Click)
            nodeView.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Set as Start Node"), false, () => SetStartNode(nodeView.Guid));
                menu.AddItem(new GUIContent("Ping ScreenModel"), false, () => PingScreen(nodeView.Screen));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Remove Node"), false, () => RemoveNode(nodeView.Guid));
                menu.ShowAsContext();
                evt.StopPropagation();
            });

            return nodeView;
        }

        private static void UpdateNodeViewUI(ScreenNodeView nodeView, ScreenModel screen)
        {
            nodeView.Screen = screen;
            nodeView.title = screen ? screen.name : "<Missing Screen>";

            nodeView.ScreenField?.SetValueWithoutNotify(screen);

            var screenIdText = screen && screen.ScreenID ? screen.ScreenID.ToString() : "<No ScreenId>";
            if (nodeView.ScreenIdLabel != null)
                nodeView.ScreenIdLabel.text = $"ScreenId: {screenIdText}";

            if (nodeView.AssetLabel != null)
                nodeView.AssetLabel.text = screen ? "Asset: " + screen.name : "Asset: <Missing ScreenModel>";
        }

        private void SetNodeScreen(string nodeGuid, ScreenModel screen)
        {
            if (!_graph) return;

            Undo.RecordObject(_graph, "Set Node Screen");
            var so = new SerializedObject(_graph);
            so.Update();
            
            var nodesProp = so.FindProperty("nodes");
            for (var i = 0; i < nodesProp.arraySize; i++)
            {
                var n = nodesProp.GetArrayElementAtIndex(i);
                if (n.FindPropertyRelative("guid").stringValue != nodeGuid) continue;
                n.FindPropertyRelative("screen").objectReferenceValue = screen;
                break;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);

            if (_nodeViewsByGuid.TryGetValue(nodeGuid, out var nodeView))
            {
                UpdateNodeViewUI(nodeView, screen);
                nodeView.RefreshExpandedState();
                nodeView.RefreshPorts();
                ApplyNodeStyling(nodeView);
            }
        }

        private void ApplyNodeStyling(ScreenNodeView nodeView)
        {
            if (!_graph) return;

            var isStart = _graph.StartNodeGuid == nodeView.Guid;
            var isMissing = !nodeView.Screen;

            // Reset
            nodeView.titleContainer.style.backgroundColor = StyleKeyword.Null;
            nodeView.mainContainer.style.backgroundColor = StyleKeyword.Null;

            // Badge
            nodeView.titleContainer.Q<Label>("__badge")?.RemoveFromHierarchy();
            if (isStart || isMissing)
            {
                var badge = new Label(isStart ? "⭐ Start" : "⚠ Missing")
                {
                    name = "__badge",
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleRight,
                        marginLeft = 6,
                        color = isStart ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.9f, 0.4f, 1f)
                    }
                };
                nodeView.titleContainer.Add(badge);
            }

            if (isStart)
                nodeView.titleContainer.style.backgroundColor = new Color(0.15f, 0.55f, 0.25f, 0.55f);

            if (isMissing)
                nodeView.mainContainer.style.backgroundColor = new Color(0.65f, 0.15f, 0.15f, 0.25f);
        }

        private void SetStartNode(string guid)
        {
            if (!_graph) return;

            Undo.RecordObject(_graph, "Set Start Node");
            _graph.SetStartNode(guid);
            EditorUtility.SetDirty(_graph);

            foreach (var kv in _nodeViewsByGuid)
                ApplyNodeStyling(kv.Value);
        }

        #endregion

        #region Drag & Drop Handlers

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (_graph == null) return;

            var refs = DragAndDrop.objectReferences;
            var hasScreenModel = refs != null && refs.AsValueEnumerable().OfType<ScreenModel>().Any();

            if (!hasScreenModel) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (_graph == null) return;

            var refs = DragAndDrop.objectReferences;
            if (refs == null || refs.Length == 0) return;

            var screens = refs.OfType<ScreenModel>().ToList();
            if (screens.Count == 0) return;

            DragAndDrop.AcceptDrag();

            var worldPos = evt.mousePosition;
            var localPos = contentViewContainer.WorldToLocal(worldPos);
            var offset = Vector2.zero;

            foreach (var t in screens)
            {
                AddScreenNodeAtPosition(localPos + offset, t);
                offset += new Vector2(34, 22);
            }

            EditorApplication.delayCall += () => { PopulateView(_graph); };
            evt.StopPropagation();
        }

        private void NodeDragUpdateCallback(DragUpdatedEvent evt)
        {
            if (_graph == null) return;
            var refs = DragAndDrop.objectReferences;
            if (refs == null || !refs.OfType<ScreenModel>().Any()) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        #endregion

        #region Edge & Port Management

        private Port CreatePort(Node node, Direction direction, Port.Capacity capacity)
        {
            var port = node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(bool));
            port.portColor = Color.white;
            port.AddManipulator(new EdgeConnector<Edge>(new EdgeConnectorListener(OnEdgeMouseDown)));
            return port;
        }

        private static void BindCallbackOnEdge(Edge edge, EventCallback<MouseDownEvent> onEdgeMouseDown)
        {
            if (edge == null || edge.viewDataKey == EdgeBoundViewDataKey) return;

            edge.viewDataKey = EdgeBoundViewDataKey;
            edge.RegisterCallback(onEdgeMouseDown, TrickleDown.TrickleDown);
            edge.AddManipulator(new EdgeManipulator());
        }

        private void OnEdgeMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not Edge edge) return;

            ClearSelection();
            AddToSelection(edge);
            evt.StopPropagation();
        }

        private sealed class EdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly EventCallback<MouseDownEvent> _onEdgeMouseDown;

            public EdgeConnectorListener(EventCallback<MouseDownEvent> onEdgeMouseDown)
            {
                _onEdgeMouseDown = onEdgeMouseDown;
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position) { /* Ignore */ }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                BindCallbackOnEdge(edge, _onEdgeMouseDown);
            }
        }

        #endregion

        #region Helpers

        private void ValidateGraph()
        {
            if (!_graph) return;

            var messages = new List<string>();

            if (string.IsNullOrEmpty(_graph.StartNodeGuid))
                messages.Add("❌ Missing Start Node.");

            messages.AddRange(_graph.Nodes.AsValueEnumerable().Where(node => node.Screen == null)
                .Select(node => $"❌ Node {node.Guid} missing ScreenModel.").ToList());

            messages.AddRange(_graph.Transitions.AsValueEnumerable()
                .Where(t => string.IsNullOrWhiteSpace(t.EventName))
                .Select(t => $"❌ Transition {t.FromNodeGuid} -> {t.ToNodeGuid} has empty EventName.").ToList());

            if (messages.Count == 0)
                messages.Add("✅ Graph looks OK.");

            EditorUtility.DisplayDialog("ScreenFlow Graph Validation", string.Join("\n", messages), "OK");
        }

        private static StickyNote CreateEntryPointHint()
        {
            var note = new StickyNote
            {
                title = "ScreenFlow",
                contents = "Right-click to add nodes.\nDrag from Out -> In to create transitions.",
                theme = StickyNoteTheme.Classic,
                fontSize = StickyNoteFontSize.Small
            };
            note.SetPosition(new Rect(new Vector2(10, 10), new Vector2(280, 80)));
            note.capabilities &= ~Capabilities.Deletable;
            note.capabilities &= ~Capabilities.Selectable;
            return note;
        }

        private static void PingScreen(ScreenModel screen)
        {
            if (!screen) return;
            EditorGUIUtility.PingObject(screen);
            Selection.activeObject = screen;
        }

        #endregion
    }
}