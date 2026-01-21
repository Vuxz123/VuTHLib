using Core.GameCycle.Screen;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Core.GameCycle.ScreenFlow.Editor.Graph
{
    public class ScreenNodeView : Node
    {
        public string Guid;
        public ScreenModel Screen;

        public Port InputPort;
        public Port OutputPort;

        // UI refs (bind once)
        public ObjectField ScreenField;
        public Label ScreenIdLabel;
        public Label AssetLabel;
    }
}