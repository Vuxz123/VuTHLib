using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Window.UI
{
    /// <summary>
    /// Tabs (or sub-pages) inside a window: one GameObject active at a time. The back button returns to the previous tab
    /// before it closes the window, and each opening starts on the default tab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WindowTabGroup : MonoBehaviour, IWindowBackHandler, IWindowOpenHandler
    {
        [Tooltip("Tab contents; exactly one is active.")]
        [SerializeField] private GameObject[] tabs = Array.Empty<GameObject>();

        [SerializeField, Min(0)] private int defaultTab;

        [Tooltip("Back returns to the previously selected tab before closing the window.")]
        [SerializeField] private bool backReturnsToPreviousTab = true;

        [Tooltip("Each opening of the window starts on the default tab, with an empty history.")]
        [SerializeField] private bool resetOnOpen = true;

        private readonly List<int> _history = new();
        private int _current = -1;

        /// <summary>Index of the selected tab (-1 before the first selection).</summary>
        public int CurrentTab => _current;

        public int TabCount => tabs.Length;

        /// <summary>Raised with the new tab index.</summary>
        public event Action<int> TabChanged;

        private void Awake()
        {
            if (_current < 0) Select(defaultTab, remember: false);
        }

        /// <summary>Shows tab <paramref name="index"/>; the current one is remembered for the back button.</summary>
        public void SelectTab(int index)
        {
            Select(index, remember: true);
        }

        private void Select(int index, bool remember)
        {
            if (tabs.Length == 0) return;
            index = Mathf.Clamp(index, 0, tabs.Length - 1);
            if (index == _current) return;

            if (remember && _current >= 0) _history.Add(_current);

            for (var i = 0; i < tabs.Length; i++)
            {
                if (tabs[i]) tabs[i].SetActive(i == index);
            }

            _current = index;
            TabChanged?.Invoke(index);
        }

        /// <summary>Returns to the previous tab; false when there is none.</summary>
        public bool TryGoBack()
        {
            if (_history.Count == 0) return false;

            var previous = _history[^1];
            _history.RemoveAt(_history.Count - 1);
            Select(previous, remember: false);
            return true;
        }

        bool IWindowBackHandler.HandleBack() => backReturnsToPreviousTab && TryGoBack();

        void IWindowOpenHandler.OnWindowOpening()
        {
            if (!resetOnOpen && _current >= 0) return;

            _history.Clear();
            _current = -1;
            Select(defaultTab, remember: false);
        }
    }
}
