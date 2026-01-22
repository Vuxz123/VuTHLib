using UnityEngine;

namespace Common.Editor.UI.DragableGrid
{
    public sealed class ImGuiDragPayload
    {
        public int Id { get; }
        public Object Object { get; }
        public System.Type ObjectType { get; }
        public string Label { get; }
        public string Tag { get; }
        public Rect SourceRect { get; }

        public ImGuiDragPayload(int id, Object obj, System.Type type, string label, string tag, Rect sourceRect)
        {
            Id = id;
            Object = obj;
            ObjectType = type;
            Label = label;
            Tag = tag;
            SourceRect = sourceRect;
        }
    }
}