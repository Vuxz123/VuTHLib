using System;
using System.Collections.Generic;
using Common.SharedLib.Log;
using Core.GameCycle.Screen;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace Core.GameCycle.ScreenFlow.Editor.Graph
{
    public class ScreenFlowGraphView : GraphView, IDisposable
    {
        private readonly ScreenFlowGraphEditorWindow _window;

        private ScreenFlowGraph _graph;

        private readonly Dictionary<string, ScreenNodeView> _nodeViewsByGuid = new();

        private bool _isRebuildingView;

        private const string EdgeBoundViewDataKey = "__sf_edge_bound__";

        public ScreenFlowGraphView(ScreenFlowGraphEditorWindow window)
        {
            _window = window;

            style.flexGrow = 1;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new EdgeManipulator());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;

            AddElement(CreateEntryPointHint());

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        public void PopulateView(ScreenFlowGraph graph)
        {
            _graph = graph;

            _isRebuildingView = true;
            try
            {
                // Clear existing view elements without triggering graph mutation callbacks.
                var elements = new List<GraphElement>();
                foreach (var e in graphElements)
                {
                    if (e is GraphElement ge)
                        elements.Add(ge);
                }

                DeleteElements(elements);

                _nodeViewsByGuid.Clear();

                if (!graph)
                    return;

                // Nodes
                foreach (var node in graph.Nodes)
                {
                    var nodeView = CreateNodeView(node);
                    AddElement(nodeView);
                }

                // Edges
                foreach (var transition in graph.Transitions)
                {
                    if (!_nodeViewsByGuid.TryGetValue(transition.FromNodeGuid, out var fromNode))
                        continue;
                    if (!_nodeViewsByGuid.TryGetValue(transition.ToNodeGuid, out var toNode))
                        continue;

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

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            evt.menu.AppendAction("Add Screen Node", action => AddScreenNodeAt(action.eventInfo.mousePosition),
                DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Validate Graph", _ => ValidateGraph(), DropdownMenuAction.AlwaysEnabled);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (_graph == null)
                return;

            bool hasScreenModel = false;
            var refs = DragAndDrop.objectReferences;
            if (refs != null)
            {
                for (int i = 0; i < refs.Length; i++)
                {
                    if (refs[i] is ScreenModel)
                    {
                        hasScreenModel = true;
                        break;
                    }
                }
            }

            if (!hasScreenModel)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (_graph == null)
                return;

            var refs = DragAndDrop.objectReferences;
            if (refs == null || refs.Length == 0)
                return;

            // Gather screen models
            var screens = new List<ScreenModel>();
            foreach (var t in refs)
            {
                if (t is ScreenModel sm)
                    screens.Add(sm);
            }

            if (screens.Count == 0)
                return;

            DragAndDrop.AcceptDrag();

            var worldPos = evt.mousePosition;
            var localPos = contentViewContainer.WorldToLocal(worldPos);

            // Slight offset per node so multiple drops don't stack perfectly.
            var offset = Vector2.zero;
            foreach (var t in screens)
            {
                AddScreenNodeAtPosition(localPos + offset, t);
                offset += new Vector2(34, 22);
            }

            // Delay refresh one tick to avoid interrupting UIElements object picker / graph interactions.
            EditorApplication.delayCall += () => { PopulateView(_graph); };

            evt.StopPropagation();
        }

        private void AddScreenNodeAtPosition(Vector2 position, ScreenModel screen)
        {
            if (_graph == null)
                return;

            // Avoid duplicates (same ScreenModel) - MVP behavior.
            if (screen != null)
            {
                foreach (var n in _graph.Nodes)
                {
                    if (n.Screen == screen)
                        return;
                }
            }

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

        private void AddScreenNodeAt(Vector2 mousePosition)
        {
            if (_graph == null)
                return;

            var worldPos = mousePosition;
            var localPos = contentViewContainer.WorldToLocal(worldPos);

            AddScreenNodeAtPosition(localPos, null);

            EditorApplication.delayCall += () => { PopulateView(_graph); };
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

            // Body (bind once)
            if (nodeView.ScreenField == null)
            {
                nodeView.ScreenField = new UnityEditor.UIElements.ObjectField("Screen")
                {
                    objectType = typeof(ScreenModel),
                    allowSceneObjects = false,
                };
                nodeView.ScreenField.RegisterValueChangedCallback(evt =>
                {
                    if (_graph == null)
                        return;
                    SetNodeScreen(nodeView.Guid, evt.newValue as ScreenModel);
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

            // Drag & drop ScreenModel onto node to assign
            nodeView.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (_graph == null)
                    return;

                var refs = DragAndDrop.objectReferences;
                if (refs == null)
                    return;

                for (int i = 0; i < refs.Length; i++)
                {
                    if (refs[i] is ScreenModel)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        evt.StopPropagation();
                        break;
                    }
                }
            });

            nodeView.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (_graph == null)
                    return;

                var refs = DragAndDrop.objectReferences;
                if (refs == null)
                    return;

                ScreenModel dropped = null;
                for (int i = 0; i < refs.Length; i++)
                {
                    if (refs[i] is ScreenModel sm)
                    {
                        dropped = sm;
                        break;
                    }
                }

                if (dropped == null)
                    return;

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

            // Right-click menu
            nodeView.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Set as Start Node"), false, () => SetStartNode(nodeView.Guid));
                    menu.AddItem(new GUIContent("Ping ScreenModel"), false, () => PingScreen(nodeView.Screen));
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Remove Node"), false, () => RemoveNode(nodeView.Guid));
                    menu.ShowAsContext();
                    evt.StopPropagation();
                }
            });

            // Left-click select -> inspector
            nodeView.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                if (_graph == null)
                    return;

                ScreenFlowNode nodeData = null;
                foreach (var n in _graph.Nodes)
                {
                    if (n.Guid == nodeView.Guid)
                    {
                        nodeData = n;
                        break;
                    }
                }

                if (nodeData != null)
                    _window.ShowSelectionInspector(ScreenNodeSelectionProxy.Create(_graph, nodeData));
            });

            return nodeView;
        }

        private void UpdateNodeViewUI(ScreenNodeView nodeView, ScreenModel screen)
        {
            nodeView.Screen = screen;
            nodeView.title = screen != null ? screen.name : "<Missing Screen>";

            if (nodeView.ScreenField != null)
                nodeView.ScreenField.SetValueWithoutNotify(screen);

            var screenIdText = screen != null && screen.ScreenID != null ? screen.ScreenID.ToString() : "<No ScreenId>";
            if (nodeView.ScreenIdLabel != null)
                nodeView.ScreenIdLabel.text = $"ScreenId: {screenIdText}";

            if (nodeView.AssetLabel != null)
                nodeView.AssetLabel.text = screen != null ? "Asset: " + screen.name : "Asset: <Missing ScreenModel>";
        }

        private void SetNodeScreen(string nodeGuid, ScreenModel screen)
        {
            if (_graph == null)
                return;

            Undo.RecordObject(_graph, "Set Node Screen");
            var so = new SerializedObject(_graph);
            so.Update();
            var nodesProp = so.FindProperty("nodes");
            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                var n = nodesProp.GetArrayElementAtIndex(i);
                if (n.FindPropertyRelative("guid").stringValue != nodeGuid)
                    continue;

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

            // IMPORTANT: don't force Selection.activeObject here.
            // It can trigger EditorWindow.OnSelectionChange() and rebuild the view, which makes the field look like it can't be assigned.
        }

        private void ApplyNodeStyling(ScreenNodeView nodeView)
        {
            if (_graph == null)
                return;

            var isStart = _graph.StartNodeGuid == nodeView.Guid;
            var isMissing = nodeView.Screen == null;

            // Reset
            nodeView.titleContainer.style.backgroundColor = StyleKeyword.Null;
            nodeView.mainContainer.style.backgroundColor = StyleKeyword.Null;

            // Badge (simple label in title)
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
            {
                nodeView.titleContainer.style.backgroundColor = new Color(0.15f, 0.55f, 0.25f, 0.55f);
            }

            if (isMissing)
            {
                nodeView.mainContainer.style.backgroundColor = new Color(0.65f, 0.15f, 0.15f, 0.25f);
            }
        }

        private void SetStartNode(string guid)
        {
            if (_graph == null)
                return;

            Undo.RecordObject(_graph, "Set Start Node");
            _graph.SetStartNode(guid);
            EditorUtility.SetDirty(_graph);

            foreach (var kv in _nodeViewsByGuid)
                ApplyNodeStyling(kv.Value);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            // Only allow Output -> Input between different nodes.
            var compatible = new List<Port>();
            ports.ForEach(port =>
            {
                if (port == startPort)
                    return;
                if (port.node == startPort.node)
                    return;
                if (port.direction == startPort.direction)
                    return;
                compatible.Add(port);
            });
            return compatible;
        }

        private Port CreatePort(Node node, Direction direction, Port.Capacity capacity)
        {
            var port = node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(bool));
            port.portColor = Color.white;

            // Enable interactive edge dragging.
            port.AddManipulator(new EdgeConnector<Edge>(new EdgeConnectorListener(onEdgeMouseDown: OnEdgeMouseDown, _graph)));
            return port;
        }

        private sealed class EdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly EventCallback<MouseDownEvent> _onEdgeMouseDown;
            private readonly ScreenFlowGraph _graph;

            public EdgeConnectorListener(EventCallback<MouseDownEvent> onEdgeMouseDown, ScreenFlowGraph graph)
            {
                _onEdgeMouseDown = onEdgeMouseDown;
                _graph = graph;
            }
            
            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                // Ignore.
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge.output?.node is not ScreenNodeView fromNode || edge.input?.node is not ScreenNodeView toNode)
                {
                    this.LogError("❌ Lỗi: Không xác định được Node đầu/cuối.");
                    return;
                }
                
                Undo.RecordObject(_graph, "Add Transition");
                var so = new SerializedObject(_graph);
                so.Update();

                var transitionsProp = so.FindProperty("transitions");
                int newIndex = transitionsProp.arraySize;
                transitionsProp.arraySize++;

                var added = transitionsProp.GetArrayElementAtIndex(newIndex);
                added.FindPropertyRelative("fromNodeGuid").stringValue = fromNode.Guid;
                added.FindPropertyRelative("toNodeGuid").stringValue = toNode.Guid;
                added.FindPropertyRelative("eventName").stringValue = "";
                // Lưu ý: Đảm bảo field "condition" trong class của bạn khớp tên
                added.FindPropertyRelative("condition").objectReferenceValue = null;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_graph); // Lưu ngay lập tức

                // 3. Lấy Data vừa tạo ra để gắn vào Edge
                // Phải lấy đúng phần tử vừa add
                var createdTransition = _graph.Transitions[newIndex];

                if (createdTransition == null)
                {
                    Debug.LogError("❌ Lỗi: Data vừa tạo bị null.");
                    return;
                }

                // 4. Gắn Data (Quan trọng nhất)
                edge.userData = createdTransition;

                // 5. Setup UI cho Edge
                // (Nếu ScreenTransitionLabel gây lỗi, hãy tạm comment dòng này để test)
                edge.Add(new ScreenTransitionLabel(createdTransition));

                // 6. Đăng ký Callback
                
                // Reset key để đảm bảo callback được đăng ký mới
                edge.viewDataKey = string.Empty;
                
                BindCallbackOnEdge(edge, _onEdgeMouseDown);
            }
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_isRebuildingView || _graph == null)
                return change;
            // --- 1. Xử lý Remove (Giữ nguyên logic fallback đã gửi ở trên) ---
            if (change.elementsToRemove != null)
            {
                var toRemove = new List<GraphElement>(change.elementsToRemove);
                foreach (var element in toRemove)
                {
                    if (element is Edge edge) RemoveTransition(edge);
                    else if (element is ScreenNodeView node) RemoveNode(node.Guid);
                }
            }

            // --- 2. Xử lý Create ---
            if (change.edgesToCreate != null)
            {
                var edgesToKeep = new List<Edge>();

                foreach (var edge in change.edgesToCreate)
                {
                    ClearSelection();
                    AddToSelection(edge);
                }
            }

            // --- 3. Xử lý Move ---
            if (change.movedElements != null)
            {
                foreach (var moved in change.movedElements)
                {
                    if (moved is ScreenNodeView nodeView) PersistNodePosition(nodeView);
                }
            }

            return change;
        }

        private static void BindCallbackOnEdge(Edge edge, EventCallback<MouseDownEvent> onEdgeMouseDown)
        {
            if (edge == null)
                return;

            // Prevent double-registering.
            if (edge.viewDataKey == EdgeBoundViewDataKey)
                return;

            edge.viewDataKey = EdgeBoundViewDataKey;
            edge.RegisterCallback(onEdgeMouseDown, TrickleDown.TrickleDown);
            edge.AddManipulator(new EdgeManipulator());
        }

        private void OnEdgeMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
                return;

            if (evt.currentTarget is not Edge edge)
                return;

            ClearSelection();
            AddToSelection(edge);
            evt.StopPropagation();
        }

        private void PersistNodePosition(ScreenNodeView nodeView)
        {
            if (!_graph)
                return;

            var pos = nodeView.GetPosition().position;

            Undo.RecordObject(_graph, "Move Screen Node");
            var so = new SerializedObject(_graph);
            so.Update();
            var nodesProp = so.FindProperty("nodes");
            for (var i = 0; i < nodesProp.arraySize; i++)
            {
                var n = nodesProp.GetArrayElementAtIndex(i);
                if (n.FindPropertyRelative("guid").stringValue == nodeView.Guid)
                {
                    n.FindPropertyRelative("editorPosition").vector2Value = pos;
                    break;
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);
        }

        #region Remove
        
        private void RemoveTransition(Edge edge)
        {
            if (_isRebuildingView || !_graph) return;

            // Chuẩn bị thông tin để tìm kiếm data cần xóa
            string fromGuid = null;
            string toGuid = null;
            ScreenFlowTransition transitionToRemove = edge.userData as ScreenFlowTransition;

            // TRƯỜNG HỢP 1: Có userData (Lý tưởng)
            if (transitionToRemove != null)
            {
                fromGuid = transitionToRemove.FromNodeGuid;
                toGuid = transitionToRemove.ToNodeGuid;
            }
            // TRƯỜNG HỢP 2: UserData bị NULL (Fallback)
            else
            {
                // Cố gắng lấy GUID từ các Node mà edge đang nối vào
                var outputNode = edge.output?.node as ScreenNodeView;
                var inputNode = edge.input?.node as ScreenNodeView;

                if (outputNode != null) fromGuid = outputNode.Guid;
                if (inputNode != null) toGuid = inputNode.Guid;

                Debug.LogWarning(
                    $"Cảnh báo: Xóa Edge nhưng userData bị null. Đang thử xóa dựa trên Node ID: {fromGuid} -> {toGuid}");
            }

            // Nếu vẫn không xác định được data để xóa thì chịu
            if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid))
            {
                Debug.LogError("Không thể xóa Transition vì không xác định được thông tin Node nguồn/đích.");
                return;
            }

            // --- Thực hiện xóa trong Data ---
            Undo.RecordObject(_graph, "Remove Transition");
            var so = new SerializedObject(_graph);
            so.Update();
            var transitionsProp = so.FindProperty("transitions");

            bool found = false;
            for (int i = 0; i < transitionsProp.arraySize; i++)
            {
                var t = transitionsProp.GetArrayElementAtIndex(i);
                // So sánh dựa trên GUID thay vì object reference để an toàn hơn
                if (t.FindPropertyRelative("fromNodeGuid").stringValue == fromGuid &&
                    t.FindPropertyRelative("toNodeGuid").stringValue == toGuid)
                {
                    // Nếu có userData, check thêm điều kiện phụ (EventName...) nếu cần thiết
                    // Nhưng cơ bản From/To khớp là đủ để xác định connection trong Graph đơn giản
                    transitionsProp.DeleteArrayElementAtIndex(i);
                    found = true;
                    break;
                }
            }

            if (found)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_graph);
            }
            else
            {
                Debug.LogWarning("Logic xóa chạy nhưng không tìm thấy Data tương ứng trong Graph để xóa.");
            }
        }

        private void RemoveNode(string guid)
        {
            if (_isRebuildingView)
                return;
            if (!_graph)
                return;

            Undo.RecordObject(_graph, "Remove Screen Node");
            var so = new SerializedObject(_graph);
            so.Update();

            // Remove transitions connected to node
            var transitionsProp = so.FindProperty("transitions");
            for (int i = transitionsProp.arraySize - 1; i >= 0; i--)
            {
                var t = transitionsProp.GetArrayElementAtIndex(i);
                var from = t.FindPropertyRelative("fromNodeGuid").stringValue;
                var to = t.FindPropertyRelative("toNodeGuid").stringValue;
                if (from == guid || to == guid)
                    transitionsProp.DeleteArrayElementAtIndex(i);
            }

            // Remove node
            var nodesProp = so.FindProperty("nodes");
            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                var n = nodesProp.GetArrayElementAtIndex(i);
                if (n.FindPropertyRelative("guid").stringValue == guid)
                {
                    nodesProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            // Clear start if needed
            if (so.FindProperty("startNodeGuid").stringValue == guid)
                so.FindProperty("startNodeGuid").stringValue = string.Empty;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_graph);

            EditorApplication.delayCall += () => { PopulateView(_graph); };
        }

        #endregion

        private void ValidateGraph()
        {
            if (_graph == null)
                return;

            var messages = new List<string>();

            if (string.IsNullOrEmpty(_graph.StartNodeGuid))
                messages.Add("❌ Missing Start Node.");

            foreach (var node in _graph.Nodes)
            {
                if (node.Screen == null)
                    messages.Add($"❌ Node {node.Guid} missing ScreenModel.");
            }

            foreach (var t in _graph.Transitions)
            {
                if (string.IsNullOrWhiteSpace(t.EventName))
                    messages.Add($"❌ Transition {t.FromNodeGuid} -> {t.ToNodeGuid} has empty EventName.");
            }

            if (messages.Count == 0)
                messages.Add("✅ Graph looks OK.");

            EditorUtility.DisplayDialog("ScreenFlow Graph Validation", string.Join("\n", messages), "OK");
        }

        private StickyNote CreateEntryPointHint()
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

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);

            if (_graph == null)
            {
                _window.ShowSelectionInspector(null);
                return;
            }

            if (selectable is Edge { userData: ScreenFlowTransition transition })
            {
                _window.ShowSelectionInspector(ScreenTransitionSelectionProxy.Create(_graph, transition));
                return;
            }

            if (selectable is ScreenNodeView nodeView)
            {
                var nodeData = _graph.Nodes.AsValueEnumerable()
                    .FirstOrDefault(n => n.Guid == nodeView.Guid);

                if (nodeData != null)
                {
                    _window.ShowSelectionInspector(ScreenNodeSelectionProxy.Create(_graph, nodeData));
                    return;
                }
            }

            _window.ShowSelectionInspector(null);
        }

        private static void PingScreen(ScreenModel screen)
        {
            if (screen == null)
                return;
            EditorGUIUtility.PingObject(screen);
            Selection.activeObject = screen;
        }

        public void Dispose()
        {
            // nothing yet
        }
    }
}