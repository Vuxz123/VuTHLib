using UnityEngine;

namespace Core.Window.Profile
{
    public class WindowProfile : ScriptableObject
    {
        [Header("Canvas Settings")]
        public int screenBaseSortingOrder = 0;
        public int popupBaseSortingOrder = 1000;
        public int systemBaseSortingOrder = 2000;
        public int sortingOrderStep = 10;
        
        [Header("Transitions")]
        public float defaultTransitionDuration = 0.3f;
        public bool useTransitionsInEditor = true;
        
        [Header("Input Blocking")]
        public bool blockInputDuringTransitions = true;
        public float minBlockDuration = 0.1f;
        
        [Header("Memory Management")]
        public int maxCachedWindows = 10;
        public float windowCacheTimeout = 60f;
        
        [Header("Debug")]
        public bool enableDebugLogs = true;
        public bool showUIStackInHierarchy = true;
    }
}