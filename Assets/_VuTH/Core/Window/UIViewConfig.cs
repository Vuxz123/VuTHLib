using System;
using UnityEngine;

namespace _VuTH.Core.Window
{
    [Serializable]
    public class UIViewConfig
    {
        public string id;
        public GameObject prefab;
        public UILayer layer;
        public WindowType windowType = WindowType.Popup;
        public bool cacheable = true;
        public bool blockInput = true;
        public bool closeOnBackPress = true;
        public string transitionPreset = "Scale";
    }
}