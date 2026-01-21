using System;
using Core.GameCycle.ScreenFlow.Editor.Graph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.GameCycle.ScreenFlow.Editor
{
    public class ScreenFlowGraphEditorWindow : EditorWindow
    {
        private const string GraphViewName = "Screen Flow Graph";

        private ScreenFlowGraph graph;
        private ScreenFlowGraphView graphView;

        private VisualElement inspectorRoot;
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
            graphView = new Graph.ScreenFlowGraphView(this);
            graphView.name = GraphViewName;
            graphView.StretchToParentSize();
            graphView.style.flexGrow = 1;
            root.Add(graphView);

            // Right: inspector
            inspectorRoot = new VisualElement
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
            root.Add(inspectorRoot);

            // Toolbar (top of inspector)
            var graphField = new ObjectField("Graph")
            {
                objectType = typeof(ScreenFlowGraph),
                value = graph,
                allowSceneObjects = false
            };
            graphField.RegisterValueChangedCallback(evt => SetGraph(evt.newValue as ScreenFlowGraph));
            inspectorRoot.Add(graphField);

            inspectorRoot.Add(new Label("Select a node/transition to edit."));

            if (graph != null)
                graphView.PopulateView(graph);
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
            graphView?.Dispose();

            if (_activeSelectionEditor != null)
            {
                DestroyImmediate(_activeSelectionEditor);
                _activeSelectionEditor = null;
            }
        }

        private void SetGraph(ScreenFlowGraph newGraph)
        {
            graph = newGraph;
            if (graphView != null)
            {
                graphView.PopulateView(graph);
                ShowSelectionInspector(null);
            }
        }

        internal void ShowSelectionInspector(UnityEngine.Object selection)
        {
            if (inspectorRoot == null)
                return;

            // Clear everything except the first ObjectField (Graph picker)
            while (inspectorRoot.childCount > 1)
                inspectorRoot.RemoveAt(1);

            if (_activeSelectionEditor != null)
            {
                DestroyImmediate(_activeSelectionEditor);
                _activeSelectionEditor = null;
            }

            if (selection == null)
            {
                inspectorRoot.Add(new Label("Select a node/transition to edit."));
                return;
            }

            _activeSelectionEditor = UnityEditor.Editor.CreateEditor(selection);
            if (_activeSelectionEditor == null)
            {
                inspectorRoot.Add(new Label($"No inspector for {selection.GetType().Name}"));
                return;
            }

            // UIElements inspector (no IMGUIContainer)
            var inspector = new InspectorElement(_activeSelectionEditor);
            inspectorRoot.Add(inspector);
        }
    }
}
