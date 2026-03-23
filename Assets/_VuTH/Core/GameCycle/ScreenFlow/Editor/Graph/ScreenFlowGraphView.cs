using System;
using System.Collections.Generic;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Core;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = System.Diagnostics.Debug;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
{
    public sealed class ScreenFlowGraphView : GraphView
    {
        private readonly ScreenFlowGraphEditorWindow _window;
        private readonly Dictionary<string, ScreenNodeView> _nodeViewsByGuid = new(StringComparer.Ordinal);
        private ScreenFlowGraphMutator _mutator;
        private ScreenFlowEdgeHandler _edgeHandler;
        private ScreenFlowGraph _graph;
        private bool _isRefreshing;
        private bool _wasDraggingOverGraph; // For ScreenModel asset drag & drop

        public ScreenFlowGraphView(ScreenFlowGraphEditorWindow window)
        {
            _window = window;

            style.flexGrow = 1f;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;

            // Refresh edge labels during node drag via Manipulator pattern
            this.AddManipulator(new EdgeLabelRefreshManipulator(
                () => _edgeHandler?.RefreshAllEdgeLabelPositions()));

            // ScreenModel drag & drop: poll DragAndDrop state for asset drops
            EditorApplication.update += OnScreenModelsDragPoll;
        }

        public void SetGraph(ScreenFlowGraph graph)
        {
            _graph = graph;
            _mutator = new ScreenFlowGraphMutator(_graph, () => ScreenFlowGraphEditorWindow.NotifyGraphChanged(_graph));
            _mutator.SetOnGraphChanged(() => ScreenFlowGraphEditorWindow.NotifyGraphChanged(_graph));
            _edgeHandler = new ScreenFlowEdgeHandler(_graph, _mutator);
            _edgeHandler.SetOnSelectionChanged(OnEdgeSelectionChanged);
            RefreshView();
        }

        private void OnEdgeSelectionChanged()
        {
            if (_graph == null || _isRefreshing)
            {
                _window.ShowSelectionInspector(null);
                return;
            }

            var selected = selection;
            if (selected.Count != 1) return;

            if (selected[0] is Edge { userData: ScreenFlowTransition transition })
            {
                _window.ShowSelectionInspector(ScreenTransitionSelectionProxy.Create(_graph, transition));
                return;
            }

            _window.ShowSelectionInspector(null);
        }

        public void Cleanup()
        {
            EditorApplication.update -= OnScreenModelsDragPoll;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            evt.menu.AppendAction("Add Screen Node", action => AddNodeAtMouse(action.eventInfo.mousePosition), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Frame All", _ => FrameAll(), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Validate Graph", _ => _edgeHandler?.ValidateGraph(), DropdownMenuAction.AlwaysEnabled);
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

            if (!_graph || _isRefreshing)
            {
                _window.ShowSelectionInspector(null);
                return;
            }

            switch (selectable)
            {
                case ScreenNodeView nodeView when TryGetNode(nodeView.Guid, out var node):
                    _window.ShowSelectionInspector(ScreenNodeSelectionProxy.Create(_graph, node));
                    return;
                case Edge { userData: ScreenFlowTransition transition }:
                    _window.ShowSelectionInspector(ScreenTransitionSelectionProxy.Create(_graph, transition));
                    return;
                default:
                    _window.ShowSelectionInspector(null);
                    break;
            }
        }

        private void RefreshView()
        {
            _isRefreshing = true;

            try
            {
                DeleteElements(graphElements.ToList());
                _nodeViewsByGuid.Clear();
                _edgeHandler?.ClearEdges();

                AddElement(CreateHintNote());

                if (!_graph) return;

                foreach (var node in _graph.Nodes)
                {
                    if (node == null) continue;

                    var nodeView = CreateNodeView(node);
                    _nodeViewsByGuid[node.Guid] = nodeView;
                    AddElement(nodeView);
                }

                foreach (var transition in _graph.Transitions)
                {
                    if (transition == null) continue;
                    if (!_nodeViewsByGuid.TryGetValue(transition.FromNodeGuid, out var fromNode)) continue;
                    if (!_nodeViewsByGuid.TryGetValue(transition.ToNodeGuid, out var toNode)) continue;

                    var edge = fromNode.OutputPort.ConnectTo(toNode.InputPort);
                    this.Assert(_edgeHandler != null, nameof(_edgeHandler) + " != null");
                    Debug.Assert(_edgeHandler != null, nameof(_edgeHandler) + " != null");
                    _edgeHandler.RegisterEdge(edge, transition);
                    AddElement(edge);
                }
            }
            finally
            {
                _isRefreshing = false;
            }

            RestorePendingSelection();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_isRefreshing || !_graph) return change;

            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    switch (element)
                    {
                        case Edge edge:
                            _edgeHandler.OnTransitionRemoved(edge);
                            break;
                        case ScreenNodeView nodeView:
                            _mutator.RemoveNode(nodeView.Guid);
                            break;
                    }
                }
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (!edge.isGhostEdge)
                    {
                        if (edge.output?.node is ScreenNodeView fromNode && edge.input?.node is ScreenNodeView toNode)
                        {
                            _edgeHandler.OnTransitionCreated(fromNode.Guid, toNode.Guid);
                        }
                    }
                }
            }

            if (change.movedElements != null)
            {
                foreach (var movedElement in change.movedElements)
                {
                    if (movedElement is ScreenNodeView nodeView)
                    {
                        _mutator.PersistNodePosition(nodeView.Guid, nodeView.GetPosition().position);
                    }
                }

                // After nodes are moved, all edge labels need to recalculate positions
                _edgeHandler.RefreshAllEdgeLabelPositions();
            }

            return change;
        }

        private void AddNodeAtMouse(Vector2 mousePosition)
        {
            if (!_graph) return;
            var localPosition = contentViewContainer.WorldToLocal(mousePosition);
            _mutator.AddNode(localPosition, null);
        }

        private ScreenNodeView CreateNodeView(ScreenFlowNode node)
        {
            var nodeView = new ScreenNodeView(
                node.Guid,
                guid => _mutator.SetStartNode(guid),
                screen => _mutator.SetNodeScreen(node.Guid, screen),
                guid => _mutator.RemoveNode(guid),
                PingScreenForNode);

            nodeView.Bind(node);
            ApplyNodeStyling(nodeView);

            return nodeView;
        }

        private void PingScreenForNode(string nodeGuid)
        {
            if (!TryGetNode(nodeGuid, out var node) || !node.Screen) return;
            EditorGUIUtility.PingObject(node.Screen);
            Selection.activeObject = node.Screen;
        }

        private void RestorePendingSelection()
        {
            if (_mutator == null) return;

            if (_mutator.PendingSelectedNodeGuid != null &&
                _nodeViewsByGuid.TryGetValue(_mutator.PendingSelectedNodeGuid, out var nodeView))
            {
                ClearSelection();
                AddToSelection(nodeView);
                _mutator.PendingSelectedNodeGuid = null;
                return;
            }

            if (_mutator.PendingSelectedTransition != null)
            {
                var edge = _edgeHandler.GetEdgeForTransition(_mutator.PendingSelectedTransition);
                if (edge != null)
                {
                    ClearSelection();
                    AddToSelection(edge);
                    _mutator.PendingSelectedTransition = null;
                }
            }
        }

        private bool TryGetNode(string nodeGuid, out ScreenFlowNode node)
        {
            if (_graph != null)
            {
                foreach (var candidate in _graph.Nodes)
                {
                    if (candidate == null || candidate.Guid != nodeGuid) continue;
                    node = candidate;
                    return true;
                }
            }

            node = null;
            return false;
        }

        private void ApplyNodeStyling(ScreenNodeView nodeView)
        {
            var isStartNode = _graph != null && _graph.StartNodeGuid == nodeView.Guid;
            var isMissingScreen = !nodeView.Screen;
            nodeView.ApplyState(isStartNode, isMissingScreen);
        }

        private static StickyNote CreateHintNote()
        {
            var note = new StickyNote
            {
                title = "ScreenFlow",
                contents = "Right click to add nodes.\nDrag ScreenModel assets into the graph or onto a node.",
                theme = StickyNoteTheme.Classic,
                fontSize = StickyNoteFontSize.Small
            };

            note.SetPosition(new Rect(new Vector2(10f, 10f), new Vector2(320f, 82f)));
            note.capabilities &= ~Capabilities.Deletable;
            note.capabilities &= ~Capabilities.Selectable;
            return note;
        }

        private void OnScreenModelsDropped(Vector2 localPosition, List<ScreenModel> screens)
        {
            if (_graph == null) return;

            var offset = Vector2.zero;
            foreach (var t in screens)
            {
                _mutator.AddNode(localPosition + offset, t);
                offset += new Vector2(36f, 24f);
            }
        }

        private static List<ScreenModel> GetDraggedScreenModels()
        {
            var screens = new List<ScreenModel>();
            var objectReferences = DragAndDrop.objectReferences;
            if (objectReferences == null) return screens;

            foreach (var t in objectReferences)
            {
                if (t is ScreenModel screen)
                {
                    screens.Add(screen);
                }
            }

            return screens;
        }

        private void OnScreenModelsDragPoll()
        {
            if (_graph == null) return;

            var evt = Event.current;
            var isDragUpdated = evt?.type == EventType.DragUpdated;
            var isDragPerform = evt?.type == EventType.DragPerform;
            var isDragging = isDragUpdated || isDragPerform;
            var screens = GetDraggedScreenModels();
            var isOverGraph = worldBound.Contains(evt?.mousePosition ?? Vector2.zero);

            if (isDragging && screens.Count > 0 && isOverGraph)
            {
                // Show valid drop indicator while hovering over graph
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                _wasDraggingOverGraph = true;

                // Update edge label positions in real-time during drag
                _edgeHandler.RefreshAllEdgeLabelPositions();
            }
            else if (_wasDraggingOverGraph && isDragPerform)
            {
                // Drag ended while over graph → accept the drop
                if (screens.Count > 0)
                {
                    DragAndDrop.AcceptDrag();
                    var localPos = contentViewContainer.WorldToLocal(evt?.mousePosition ?? Vector2.zero);
                    OnScreenModelsDropped(localPos, screens);
                }
                _wasDraggingOverGraph = false;
            }
            else if (!isDragging)
            {
                // Drag ended outside graph or cancelled
                _wasDraggingOverGraph = false;
            }
        }
    }
}