using System.Collections.Generic;
using _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor
{
    public class ScreenFlowGraphEditorWindow : EditorWindow
    {
        private const float InspectorWidth = 320f;
        private const string EmptyInspectorText = "Select a node or transition to edit.";

        private static readonly List<ScreenFlowGraphEditorWindow> OpenWindows = new();

        // Serialized so the window keeps its graph across domain reloads (script recompiles).
        [SerializeField] private ScreenFlowGraph _graph;

        private ScreenFlowGraphView _graphView;
        private VisualElement _inspectorRoot;
        private ObjectField _graphField;
        private UnityEditor.Editor _activeSelectionEditor;
        private Object _activeSelectionProxy;
        private bool _refreshQueued;

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

        internal static void NotifyGraphChanged(ScreenFlowGraph graph)
        {
            if (!graph) return;

            for (var i = 0; i < OpenWindows.Count; i++)
            {
                var window = OpenWindows[i];
                if (window == null || window._graph != graph) continue;
                window.QueueGraphRefresh();
            }
        }

        private void OnEnable()
        {
            if (!OpenWindows.Contains(this))
            {
                OpenWindows.Add(this);
            }

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            OpenWindows.Remove(this);
            Undo.undoRedoPerformed -= OnUndoRedo;

            _graphView?.Cleanup();
            _graphView = null;

            DestroyActiveSelection();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;

            _graphView = new ScreenFlowGraphView(this)
            {
                name = "Screen Flow Graph"
            };
            _graphView.style.flexGrow = 1f;
            root.Add(_graphView);

            _inspectorRoot = new VisualElement
            {
                name = "inspector",
                style =
                {
                    width = InspectorWidth,
                    flexShrink = 0,
                    borderLeftWidth = 1,
                    borderLeftColor = new Color(0f, 0f, 0f, 0.35f),
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = EditorGUIUtility.isProSkin
                        ? new Color(0.15f, 0.15f, 0.15f, 1f)
                        : new Color(0.82f, 0.82f, 0.82f, 1f),
                }
            };
            root.Add(_inspectorRoot);

            _graphField = new ObjectField("Graph")
            {
                objectType = typeof(ScreenFlowGraph),
                allowSceneObjects = false,
                value = _graph
            };
            _graphField.RegisterValueChangedCallback(evt => SetGraph(evt.newValue as ScreenFlowGraph));
            _inspectorRoot.Add(_graphField);
            _inspectorRoot.Add(new Label(EmptyInspectorText));

            RefreshGraphView();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is ScreenFlowGraph selected && selected != _graph)
            {
                SetGraph(selected);
            }
        }

        private void OnUndoRedo()
        {
            // Undo/redo changes the graph asset behind the view's back: rebuild it and drop a possibly stale inspector.
            if (!_graph) return;
            ShowSelectionInspector(null);
            QueueGraphRefresh();
        }

        internal void SetGraph(ScreenFlowGraph graph)
        {
            _graph = graph;

            if (_graphField != null)
            {
                _graphField.SetValueWithoutNotify(graph);
            }

            RefreshGraphView();
            ShowSelectionInspector(null);
        }

        internal void QueueGraphRefresh()
        {
            if (_refreshQueued) return;
            _refreshQueued = true;

            EditorApplication.delayCall += () =>
            {
                _refreshQueued = false;
                if (this == null) return;
                RefreshGraphView();
            };
        }

        internal void ShowSelectionInspector(Object selection)
        {
            if (_inspectorRoot == null)
            {
                // No UI yet: still own (and free) the proxy.
                if (selection is ScreenNodeSelectionProxy or ScreenTransitionSelectionProxy) DestroyImmediate(selection);
                return;
            }

            while (_inspectorRoot.childCount > 1)
            {
                _inspectorRoot.RemoveAt(1);
            }

            DestroyActiveSelection();

            if (!selection)
            {
                _inspectorRoot.Add(new Label(EmptyInspectorText));
                return;
            }

            // Selection proxies are temporary HideAndDontSave objects owned by this window: freed on the next selection.
            if (selection is ScreenNodeSelectionProxy or ScreenTransitionSelectionProxy)
            {
                _activeSelectionProxy = selection;
            }

            _activeSelectionEditor = UnityEditor.Editor.CreateEditor(selection);
            if (!_activeSelectionEditor)
            {
                _inspectorRoot.Add(new Label($"No inspector for {selection.GetType().Name}"));
                return;
            }

            _inspectorRoot.Add(new InspectorElement(_activeSelectionEditor));
        }

        private void RefreshGraphView()
        {
            _graphView?.SetGraph(_graph);

            // The node / transition shown in the inspector may have been deleted (or undone) meanwhile.
            var stale = _activeSelectionProxy switch
            {
                ScreenNodeSelectionProxy node => !node.IsValid,
                ScreenTransitionSelectionProxy transition => !transition.IsValid,
                _ => false
            };
            if (stale) ShowSelectionInspector(null);
        }

        private void DestroyActiveSelection()
        {
            if (_activeSelectionEditor)
            {
                DestroyImmediate(_activeSelectionEditor);
                _activeSelectionEditor = null;
            }

            if (_activeSelectionProxy)
            {
                DestroyImmediate(_activeSelectionProxy);
                _activeSelectionProxy = null;
            }
        }
    }
}
