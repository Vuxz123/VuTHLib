using System.Collections.Generic;
using Core.GameCycle.Screen;

namespace Core.GameCycle.ScreenFlow
{
    public class ScreenFlowStateContainer
    {
        public ScreenFlowNode CurrentNode { get; private set; }
        public ScreenFlowNode PreviousNode { get; private set; }

        public ScreenModel Current => CurrentNode?.Screen;
        public ScreenModel Previous => PreviousNode?.Screen;

        public string LastEvent { get; private set; }

        /// <summary>
        /// Recent node hit-history (bounded). Oldest at index 0.
        /// </summary>
        public IReadOnlyList<ScreenFlowNode> History => _history;

        private readonly List<ScreenFlowNode> _history;
        private readonly int _historyCapacity;

        public ScreenFlowStateContainer(int historyCapacity = 32)
        {
            _historyCapacity = historyCapacity <= 0 ? 0 : historyCapacity;
            _history = _historyCapacity > 0 ? new List<ScreenFlowNode>(_historyCapacity) : new List<ScreenFlowNode>(0);
        }

        internal void Reset(ScreenFlowNode startNode)
        {
            PreviousNode = null;
            CurrentNode = startNode;
            LastEvent = null;
            _history.Clear();
            AppendHistory(startNode);
        }

        internal void Set(ScreenFlowNode nextNode, string lastEvent)
        {
            PreviousNode = CurrentNode;
            CurrentNode = nextNode;
            LastEvent = lastEvent;
            AppendHistory(nextNode);
        }

        private void AppendHistory(ScreenFlowNode node)
        {
            if (_historyCapacity <= 0 || node == null) return;

            if (_history.Count >= _historyCapacity)
                _history.RemoveAt(0);

            _history.Add(node);
        }
    }
}