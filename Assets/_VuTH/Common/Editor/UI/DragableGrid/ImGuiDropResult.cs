using UnityEngine;

namespace _VuTH.Common.Editor.UI.DragableGrid
{
    public struct ImGuiDropResult<T> where T : Object
    {
        public bool IsHovering;
        public bool CanAccept;
        public bool Performed;
        public T DroppedObject;
        public ImGuiDragPayload Payload;
    }
}