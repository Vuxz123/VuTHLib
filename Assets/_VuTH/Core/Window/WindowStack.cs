using System.Collections.Generic;

namespace _VuTH.Core.Window
{
    /// <summary>
    /// Bookkeeping for the open-window stack used by <see cref="WindowManager"/>:
    /// keeps windows in insertion order (index 0 = bottom, last = top). Pure C# (no Unity dependencies) so it can be
    /// unit tested.
    /// </summary>
    /// <remarks>
    /// Equality uses <see cref="EqualityComparer{T}.Default"/> (same as a plain List/Dictionary),
    /// and type queries use a runtime type check, so destroyed Unity objects are still matched.
    /// </remarks>
    /// <typeparam name="TView">Window view type (UIViewBase at runtime).</typeparam>
    public sealed class WindowStack<TView> where TView : class
    {
        // Index 0 = bottom, last = top.
        private readonly List<TView> _windows = new();

        /// <summary>Number of windows currently in the stack.</summary>
        public int Count => _windows.Count;

        /// <summary>Top-most window, or null when the stack is empty.</summary>
        public TView Top => _windows.Count > 0 ? _windows[^1] : null;

        /// <summary>Pushes a window on top of the stack.</summary>
        public void Push(TView window)
        {
            _windows.Add(window);
        }

        /// <summary>Removes the exact window from anywhere in the stack (not only the top).</summary>
        /// <returns>True if the window was found and removed.</returns>
        public bool Remove(TView window)
        {
            return _windows.Remove(window);
        }

        /// <summary>True if the window is currently in the stack.</summary>
        public bool Contains(TView window)
        {
            return _windows.Contains(window);
        }

        /// <summary>Snapshot of the stack, bottom to top.</summary>
        public TView[] ToArray()
        {
            return _windows.ToArray();
        }

        /// <summary>Removes all windows.</summary>
        public void Clear()
        {
            _windows.Clear();
        }

        /// <summary>True if any window in the stack is of type <typeparamref name="T"/>.</summary>
        public bool HasWindowOfType<T>() where T : class
        {
            foreach (var window in _windows)
            {
                if (window is T)
                    return true;
            }

            return false;
        }

        /// <summary>First window (from the bottom) of type <typeparamref name="T"/>, or null.</summary>
        public T GetWindowOfType<T>() where T : class
        {
            foreach (var window in _windows)
            {
                if (window is T typed)
                    return typed;
            }

            return null;
        }
    }
}
