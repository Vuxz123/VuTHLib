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

            // ScreenModel asset drag & drop onto empty graph space (nodes handle drops onto themselves).
            this.AddManipulator(new ScreenModelDropManipulator(
                GetDraggedScreenModels,
                world => contentViewContainer.WorldToLocal(world),
                OnScreenModelsDropped));
        }

        public void SetGraph(ScreenFlowGraph graph)
        {
            _graph = graph;
            _mutator = new ScreenFlowGraphMutator(_graph, () => ScreenFlowGraphEditorWindow.NotifyGraphChanged(_graph));
            _mutator.SetOnGraphChanged(() => ScreenFlowGraphEditorWindow.NotifyGraphChanged(_graph));
            _edgeHandler = new ScreenFlowEdgeHandler(_graph, _mutator);

            RefreshView();
        }



        public void Cleanup()
        {
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            if (!_isRefreshing) _window.ShowSelectionInspector(null);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            // Only on empty canvas: nodes / edges provide their own entries.
            if (evt.target is not GraphView) return;

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

            if (!string.IsNullOrEmpty(_mutator.PendingSelectedTransitionGuid))
            {
                var edge = _edgeHandler.GetEdgeForTransitionGuid(_mutator.PendingSelectedTransitionGuid);
                if (edge != null)
                {
                    ClearSelection();
                    AddToSelection(edge);
                    _mutator.PendingSelectedTransitionGuid = null;
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
    }
}