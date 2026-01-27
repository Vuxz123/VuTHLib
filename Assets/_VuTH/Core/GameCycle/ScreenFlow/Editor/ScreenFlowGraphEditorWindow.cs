using _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor
{
    public class ScreenFlowGraphEditorWindow : EditorWindow
    {
        private const string GraphViewName = "Screen Flow Graph";

        private ScreenFlowGraph _graph;
        private ScreenFlowGraphView _graphView;

        private VisualElement _inspectorRoot;
        private UnityEditor.Editor _activeSelectionEditor;

        [MenuItem("VuTH/Core/Screen Flow/Screen Flow Graph Editor")]
        public static void Open()
        {
            var window = GetWindow<ScreenFlowGraphEditorWindow>();
            window.titleContent = new GUIContent("Screen Flow");
        }

        public static void Open(ScreenFlowGraph graph)
        {
            var window = GetWindow<ScreenFlowGraphEditorWindow>();
            window.titleContent = new GUIContent("Screen Flow");
            window.SetGraph(graph);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            // Left: graph
            _graphView = new ScreenFlowGraphView(this);
            _graphView.name = GraphViewName;
            _graphView.StretchToParentSize();
            _graphView.style.flexGrow = 1;
            root.Add(_graphView);

            // Right: inspector
            _inspectorRoot = new VisualElement
            {
                name = "inspector",
                style =
                {
                    width = 320,
                    flexShrink = 0,
                    borderLeftWidth = 1,
                    borderLeftColor = new Color(0, 0, 0, 0.35f),
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1),
                }
            };
            root.Add(_inspectorRoot);

            // Toolbar (top of inspector)
            var graphField = new ObjectField("Graph")
            {
                objectType = typeof(ScreenFlowGraph),
                value = _graph,
                allowSceneObjects = false
            };
            graphField.RegisterValueChangedCallback(evt => SetGraph(evt.newValue as ScreenFlowGraph));
            _inspectorRoot.Add(graphField);

            _inspectorRoot.Add(new Label("Select a node/transition to edit."));

            if (_graph)
                _graphView.PopulateView(_graph);
        }

        private void OnSelectionChange()
        {
            // If user selects a ScreenFlowGraph asset, open it.
            if (Selection.activeObject is ScreenFlowGraph selected)
            {
                SetGraph(selected);
            }
        }

        private void OnDisable()
        {
            _graphView?.Dispose();

            if (!_activeSelectionEditor) return;
            DestroyImmediate(_activeSelectionEditor);
            _activeSelectionEditor = null;
        }

        private void SetGraph(ScreenFlowGraph newGraph)
        {
            _graph = newGraph;
            if (_graphView == null) return;
            _graphView.PopulateView(_graph);
            ShowSelectionInspector(null);
        }

        internal void ShowSelectionInspector(Object selection)
        {
            if (_inspectorRoot == null)
                return;

            // Clear everything except the first ObjectField (Graph picker)
            while (_inspectorRoot.childCount > 1)
                _inspectorRoot.RemoveAt(1);

            if (_activeSelectionEditor)
            {
                DestroyImmediate(_activeSelectionEditor);
                _activeSelectionEditor = null;
            }

            if (!selection)
            {
                _inspectorRoot.Add(new Label("Select a node/transition to edit."));
                return;
            }

            _activeSelectionEditor = UnityEditor.Editor.CreateEditor(selection);
            if (!_activeSelectionEditor)
            {
                _inspectorRoot.Add(new Label($"No inspector for {selection.GetType().Name}"));
                return;
            }

            // UIElements inspector (no IMGUIContainer)
            var inspector = new InspectorElement(_activeSelectionEditor);
            _inspectorRoot.Add(inspector);
        }
    }
}
