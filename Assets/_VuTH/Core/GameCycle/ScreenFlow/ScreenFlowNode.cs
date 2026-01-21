using System;
using UnityEngine;
using Core.GameCycle.Screen;

namespace Core.GameCycle.ScreenFlow
{

    [Serializable]
    public class ScreenFlowNode
    {
        [SerializeField] private string guid;
        [SerializeField] private ScreenModel screen;

        [SerializeField] private Vector2 editorPosition;

        public string Guid => guid;
        public ScreenModel Screen => screen;

        public Vector2 EditorPosition => editorPosition;

#if UNITY_EDITOR
        public ScreenFlowNode(string guid, ScreenModel screen, Vector2 pos)
        {
            this.guid = guid;
            this.screen = screen;
            this.editorPosition = pos;
        }

        public void SetPosition(Vector2 pos)
        {
            editorPosition = pos;
        }
#endif
    }
}