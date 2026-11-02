using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Row selection and drag-to-reorder logic for <see cref="DictionaryField"/>.
    /// The selection/insert-index/move-target math is pure and unit-testable; the IMGUI event handlers are editor-only.
    /// </summary>
    public static class DictionaryRowInteraction
    {
        /// <summary>
        /// Applies a click on row <paramref name="index"/> to <paramref name="selected"/>:
        /// ctrl/cmd toggles the row, shift selects the range from the nearest selected row, otherwise selects only that row.
        /// </summary>
        public static void ApplySelectionClick(HashSet<int> selected, int index, bool ctrl, bool shift)
        {
            if (ctrl)
            {
                if (!selected.Add(index)) selected.Remove(index);
            }
            else if (shift)
            {
                if (selected.Count == 0)
                    selected.Add(index);
                else
                {
                    int anchor = -1;
                    int minDist = int.MaxValue;
                    foreach (var sel in selected)
                    {
                        int dist = Mathf.Abs(sel - index);
                        if (dist < minDist) { minDist = dist; anchor = sel; }
                    }
                    if (anchor >= 0)
                    {
                        int a = Mathf.Min(anchor, index);
                        int b = Mathf.Max(anchor, index);
                        selected.Clear();
                        for (int iSel = a; iSel <= b; iSel++) selected.Add(iSel);
                    }
                }
            }
            else
            {
                selected.Clear();
                selected.Add(index);
            }
        }

        /// <summary>
        /// Computes the absolute insert index for a drag at <paramref name="mouseY"/>: before the first visible row whose
        /// vertical center is below the mouse, otherwise after the last visible row (capped at <paramref name="totalCount"/>).
        /// Returns 0 when there are no rows.
        /// </summary>
        public static int? ComputePreciseInsertIndex(IReadOnlyList<Rect> rowRects, IReadOnlyList<int> visibleIndices,
            int totalCount, float mouseY)
        {
            if (rowRects.Count == 0) return 0; // empty list => insert at start
            // Build list of center Y for comparison
            for (int i = 0; i < rowRects.Count; i++)
            {
                var centerY = (rowRects[i].yMin + rowRects[i].yMax) * 0.5f;
                if (mouseY < centerY)
                {
                    // Insert before this visible row => absolute index = indices[i]
                    return visibleIndices[i];
                }
            }
            // After last => place after last visible index (next index)
            int last = visibleIndices[visibleIndices.Count - 1];
            return Math.Min(last + 1, totalCount); // absolute position after last
        }

        /// <summary>
        /// Computes the y position of the insertion line for absolute insert index <paramref name="insertAbs"/>.
        /// <paramref name="emptyY"/> is used when there are no rows.
        /// </summary>
        public static float ComputeInsertLineY(IReadOnlyList<Rect> rects, IReadOnlyList<int> visibleIndices,
            int insertAbs, float emptyY)
        {
            float y;
            if (rects.Count == 0)
                y = emptyY;
            else if (insertAbs <= visibleIndices[0])
                y = rects[0].yMin - 2f;
            else if (insertAbs > visibleIndices[rects.Count - 1])
                y = rects[rects.Count - 1].yMax + 2f;
            else
            {
                // Find indices bounding insertAbs
                int lowerIdx = -1;
                int upperIdx = -1;
                for (int r = 0; r < visibleIndices.Count; r++)
                {
                    int real = visibleIndices[r];
                    if (real < insertAbs) lowerIdx = r;
                    if (real >= insertAbs) { upperIdx = r; break; }
                }
                if (upperIdx >= 0 && lowerIdx >= 0)
                {
                    y = (rects[lowerIdx].yMax + rects[upperIdx].yMin) * 0.5f;
                }
                else if (upperIdx >= 0)
                    y = rects[upperIdx].yMin - 2f;
                else // fallback
                    y = rects[rects.Count - 1].yMax + 2f;
            }

            return y;
        }

        /// <summary>
        /// Converts a drag's insert index into the destination index for a single-element move of row <paramref name="from"/>
        /// (adjusting for removal when moving downward), clamped to <c>[0, count - 1]</c> via <see cref="Mathf.Clamp(int,int,int)"/>.
        /// </summary>
        public static int ComputeMoveTarget(int from, int insertIndex, int count)
        {
            int to = insertIndex;
            if (to > from) to -= 1; // adjust target after removal when moving downward
            return Mathf.Clamp(to, 0, count - 1);
        }

        internal static void HandleRowSelection(Rect clickRect, int index, DictionaryViewState state)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || !clickRect.Contains(e.mousePosition)) return;
            var ctrl = e.control || e.command;
            var shift = e.shift;
            ApplySelectionClick(state.Selected, index, ctrl, shift);
            e.Use();
        }

        internal static void HandleRowDrag(Rect rowRect,
            int index,
            DictionaryViewState state,
            SerializedProperty keysProp,
            SerializedProperty valuesProp)
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (rowRect.Contains(e.mousePosition) && e.button == 0)
                    {
                        float localX = e.mousePosition.x - rowRect.xMin;
                        if (localX <= 20f)
                        {
                            state.DraggingIndex = index;
                            state.DragInsertIndex = index; // initialize insertion at self
                            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                            e.Use();
                        }
                    }
                    break;
                case EventType.MouseDrag:
                case EventType.MouseMove:
                    if (state.DraggingIndex.HasValue && GUIUtility.hotControl != 0 && !string.IsNullOrEmpty(state.Search))
                    {
                        // Fallback heuristic only when searching (precise reorder disabled under filter)
                        float dy = e.delta.y;
                        int current = state.DraggingIndex.Value;
                        if (Mathf.Abs(dy) > 1f)
                        {
                            if (dy < 0) state.DragInsertIndex = current; // before
                            else state.DragInsertIndex = current + 1; // after
                        }
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (state.DraggingIndex.HasValue && GUIUtility.hotControl != 0)
                    {
                        if (state.DragInsertIndex.HasValue && state.DragInsertIndex.Value != state.DraggingIndex.Value)
                        {
                            int from = state.DraggingIndex.Value;
                            int to = ComputeMoveTarget(from, state.DragInsertIndex.Value, keysProp.arraySize);
                            SerializedDictionaryOperations.MoveRow(from, to, keysProp, valuesProp);
                        }
                        state.DraggingIndex = null;
                        state.DragInsertIndex = null;
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }

        /// <summary>Updates the live insert index while dragging (only without a search filter).</summary>
        internal static void UpdatePreciseInsertIndex(DictionaryViewState state, int totalCount)
        {
            // Precise drag insertion index update (only when no search filter to avoid ambiguous reordering of filtered subset)
            if (state.DraggingIndex.HasValue && string.IsNullOrEmpty(state.Search))
            {
                var eDrag = Event.current;
                if (eDrag.type == EventType.MouseDrag || eDrag.type == EventType.MouseMove)
                {
                    state.DragInsertIndex = ComputePreciseInsertIndex(state.CurrentRowRects, state.CurrentVisibleIndices,
                        totalCount, eDrag.mousePosition.y);
                    // Request repaint for visual feedback
                    if (state.DragInsertIndex.HasValue) EditorWindow.focusedWindow?.Repaint();
                }
            }
        }

        /// <summary>Draws the drag insertion line feedback on repaint.</summary>
        internal static void DrawInsertionLine(DictionaryViewState state, Rect headerRect)
        {
            if (state.DragInsertIndex.HasValue && state.DraggingIndex.HasValue && Event.current.type == EventType.Repaint)
            {
                int insertAbs = state.DragInsertIndex.Value;
                // Determine y position relative to stored rects
                float y = ComputeInsertLineY(state.CurrentRowRects, state.CurrentVisibleIndices, insertAbs,
                    headerRect.yMax + 4f);
                var lineRect = new Rect(0, y, EditorGUIUtility.currentViewWidth, 2f);
                EditorGUI.DrawRect(lineRect, new Color(0.2f, 0.8f, 1f, 0.9f));
            }
        }
    }
}
