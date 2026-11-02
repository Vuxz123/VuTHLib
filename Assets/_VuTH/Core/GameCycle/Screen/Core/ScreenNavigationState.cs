using System.Collections.Generic;

namespace _VuTH.Core.GameCycle.Screen.Core
{
    /// <summary>
    /// Pure navigation state for <see cref="ScreenManager"/>: the base screen, the runtime navigation stack,
    /// the single override (interrupt) slot and the previous effective screen.
    /// Holds no Unity scene logic, so the "effective current" rules can be unit tested in isolation.
    /// </summary>
    public sealed class ScreenNavigationState
    {
        // Stack (runtime navigation). Top = effective current.
        private readonly Stack<ScreenModel> _screenStack = new();

        /// <summary>
        /// Base (state) screen. <c>Enter</c> changes this; <c>Pop</c> resets it to the bottom of the stack.
        /// </summary>
        public ScreenModel Base { get; set; }

        /// <summary>
        /// Override (interrupt) screen. Single slot; takes precedence over the stack and the base screen.
        /// </summary>
        public ScreenModel Override { get; set; }

        /// <summary>
        /// Previous effective screen.
        /// </summary>
        public ScreenModel Previous { get; set; }

        /// <summary>
        /// Number of screens currently on the navigation stack.
        /// </summary>
        public int StackCount => _screenStack.Count;

        /// <summary>
        /// True when the stack has a screen below the top one to return to.
        /// </summary>
        public bool CanPop => _screenStack.Count > 1;

        /// <summary>
        /// True when an override screen is active.
        /// </summary>
        public bool IsOverriding => Override != null;

        /// <summary>
        /// The effective current screen: override if active, otherwise the stack top, otherwise the base screen.
        /// </summary>
        public ScreenModel EffectiveCurrent
        {
            get
            {
                if (Override) return Override;
                return _screenStack.Count > 0 ? _screenStack.Peek() : Base;
            }
        }

        /// <summary>
        /// The bottom-most screen of the stack, or null when the stack is empty.
        /// </summary>
        public ScreenModel BottomOfStack
        {
            get
            {
                var arr = _screenStack.ToArray();
                return arr.Length > 0 ? arr[^1] : null;
            }
        }

        /// <summary>
        /// Pushes a screen on top of the navigation stack.
        /// </summary>
        public void Push(ScreenModel screen) => _screenStack.Push(screen);

        /// <summary>
        /// Pops and returns the top of the navigation stack. Throws if the stack is empty.
        /// </summary>
        public ScreenModel Pop() => _screenStack.Pop();

        /// <summary>
        /// Returns the top of the navigation stack without removing it. Throws if the stack is empty.
        /// </summary>
        public ScreenModel Peek() => _screenStack.Peek();

        /// <summary>
        /// Pops the top of the stack only if it is <paramref name="screen"/> (used to roll back a failed push).
        /// </summary>
        /// <returns>True if the top was removed.</returns>
        public bool PopIfTop(ScreenModel screen)
        {
            if (_screenStack.Count > 0 && _screenStack.Peek() == screen)
            {
                _screenStack.Pop();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clears the stack and returns its former contents ordered top to bottom.
        /// </summary>
        public ScreenModel[] TakeAll()
        {
            // Note: Stack.ToArray() returns [top..bottom]. Perfect for closing overlays first.
            var screens = _screenStack.ToArray();
            _screenStack.Clear();
            return screens;
        }

        /// <summary>
        /// Resets base, override, stack and previous to empty.
        /// </summary>
        public void Reset()
        {
            Base = null;
            Override = null;
            _screenStack.Clear();
            Previous = null;
        }
    }
}
