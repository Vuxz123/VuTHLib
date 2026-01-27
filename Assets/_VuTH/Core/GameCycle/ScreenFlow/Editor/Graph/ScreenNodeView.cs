using _VuTH.Core.GameCycle.Screen;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph
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