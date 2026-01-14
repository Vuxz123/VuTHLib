using UnityEngine;
using System.Collections.Generic;

namespace Common.Editor.UI
{
    /// <summary>
    /// Dedicated control for rendering an inventory grid with optional scrolling and scroll state management.
    /// Provides APIs to get/set/clear scroll positions and will attempt to auto-scroll to a changed cell.
    /// </summary>
    public static class InventoryGridControl
    {
        // scroll cache keyed by hash (gridId.GetHashCode() or fallback key)
        private static readonly Dictionary<int, Vector2> InventoryScroll = new Dictionary<int, Vector2>();

        /// <summary>
        /// Render a grid of items. Largely compatible signature with the old ImGuiDropControls.InventoryGrid.
        /// </summary>
        public static (int row, int column) InventoryGrid<T>(
            T[,] items,
            int rows,
            int columns,
            float cellSize,
            float cellPadding,
            bool allowSceneObjects = true,
            string dragTag = null,
            string requiredTag = null,
            System.Action<Rect, ImGuiDropResult<T>, (int row, int column)> customDrawer = null,
            string gridId = null,
            float maxVisibleHeight = 300f,
            bool allowHorizontalScroll = false,
            bool autoScrollToChanged = true
        ) where T : Object
        {
            if (items == null || rows <= 0 || columns <= 0)
                return (-1, -1);

            // Compute total grid size
            float totalWidth = columns * cellSize + (columns - 1) * cellPadding;
            float totalHeight = rows * cellSize + (rows - 1) * cellPadding;

            // Measure available width from layout
            Rect availRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true));
            float availableWidth = availRect.width;

            // Determine available height (auto or fixed)
            float availableHeight;
            bool autoHeight = maxVisibleHeight <= 0f;
            if (autoHeight)
            {
                Rect availHeightRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                availableHeight = availHeightRect.height;
            }
            else
            {
                availableHeight = maxVisibleHeight;
            }

            bool needHorizontalScroll = allowHorizontalScroll && totalWidth > availableWidth;
            bool needVerticalScroll = availableHeight > 0f && totalHeight > availableHeight;

            int key;
            if (!string.IsNullOrEmpty(gridId))
                key = gridId.GetHashCode();
            else
            {
                key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(items);
                key = key ^ (rows << 8) ^ (columns << 16);
            }

            InventoryScroll.TryGetValue(key, out var scroll);

            Rect gridRect;
            float viewW = totalWidth;
            float viewH = totalHeight;

            if (needHorizontalScroll || needVerticalScroll)
            {
                viewW = Mathf.Min(totalWidth, availableWidth);
                viewH = Mathf.Min(totalHeight, availableHeight > 0f ? availableHeight : totalHeight);

                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(viewW), GUILayout.Height(viewH));
                gridRect = GUILayoutUtility.GetRect(totalWidth, totalHeight, GUILayout.ExpandWidth(false));
            }
            else
            {
                gridRect = GUILayoutUtility.GetRect(totalWidth, totalHeight, GUILayout.ExpandWidth(false));
            }

            (int row, int column) lastChanged = (-1, -1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float x = gridRect.xMin + c * (cellSize + cellPadding);
                    float y = gridRect.yMin + r * (cellSize + cellPadding);
                    Rect cellRect = new Rect(x, y, cellSize, cellSize);

                    int rowIndex = r;
                    int colIndex = c;

                    var (newValue, _) = ImGuiDropControls.DraggableDropZone(
                        cellRect,
                        label: null,
                        currentValue: items[rowIndex, colIndex],
                        allowSceneObjects: allowSceneObjects,
                        dragTag: dragTag,
                        requiredTag: requiredTag,
                        customDrawer: customDrawer == null
                            ? null
                            : (rect, res) => customDrawer(rect, res, (rowIndex, colIndex))
                    );

                    if (!Equals(newValue, items[rowIndex, colIndex]))
                    {
                        items[rowIndex, colIndex] = newValue;
                        lastChanged = (rowIndex, colIndex);
                    }
                }
            }

            // If something changed and auto-scroll requested, ensure the changed cell is visible
            if (autoScrollToChanged && lastChanged.row >= 0)
            {
                if (needHorizontalScroll || needVerticalScroll)
                {
                    // Compute the content-relative rect of the changed cell
                    float cellX = gridRect.xMin + lastChanged.column * (cellSize + cellPadding);
                    float cellY = gridRect.yMin + lastChanged.row * (cellSize + cellPadding);

                    // gridRect.xMin/yMin is the content's position inside the layout; scroll is in content-space
                    // We want the scroll.y to be such that cellY is between scroll.y and scroll.y + viewH - cellSize
                    float contentTop = gridRect.yMin;
                    float relY = cellY - contentTop; // position within content starting at 0
                    float relX = cellX - gridRect.xMin;

                    // Adjust vertical
                    if (needVerticalScroll)
                    {
                        if (relY < scroll.y)
                        {
                            scroll.y = relY;
                        }
                        else if (relY + cellSize > scroll.y + viewH)
                        {
                            scroll.y = relY + cellSize - viewH;
                        }

                        scroll.y = Mathf.Max(0f, Mathf.Min(scroll.y, Mathf.Max(0f, totalHeight - viewH)));
                    }

                    // Adjust horizontal
                    if (needHorizontalScroll)
                    {
                        if (relX < scroll.x)
                        {
                            scroll.x = relX;
                        }
                        else if (relX + cellSize > scroll.x + viewW)
                        {
                            scroll.x = relX + cellSize - viewW;
                        }

                        scroll.x = Mathf.Max(0f, Mathf.Min(scroll.x, Mathf.Max(0f, totalWidth - viewW)));
                    }
                }
            }

            if (needHorizontalScroll || needVerticalScroll)
            {
                GUILayout.EndScrollView();
                InventoryScroll[key] = scroll;
            }

            return lastChanged;
        }

        // Scroll cache helpers
        public static void ClearAll()
        {
            InventoryScroll.Clear();
        }

        public static void Clear(string gridId)
        {
            if (string.IsNullOrEmpty(gridId)) return;
            InventoryScroll.Remove(gridId.GetHashCode());
        }

        public static void ClearForItems(object itemsReference, int rows, int columns)
        {
            if (itemsReference == null) return;
            int key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(itemsReference);
            key = key ^ (rows << 8) ^ (columns << 16);
            InventoryScroll.Remove(key);
        }

        public static Vector2 GetScroll(string gridId)
        {
            if (string.IsNullOrEmpty(gridId)) return Vector2.zero;
            InventoryScroll.TryGetValue(gridId.GetHashCode(), out var v);
            return v;
        }

        public static void SetScroll(string gridId, Vector2 scroll)
        {
            if (string.IsNullOrEmpty(gridId)) return;
            InventoryScroll[gridId.GetHashCode()] = scroll;
        }

        public static Vector2 GetScrollForItems(object itemsReference, int rows, int columns)
        {
            if (itemsReference == null) return Vector2.zero;
            int key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(itemsReference);
            key = key ^ (rows << 8) ^ (columns << 16);
            InventoryScroll.TryGetValue(key, out var v);
            return v;
        }

        public static void SetScrollForItems(object itemsReference, int rows, int columns, Vector2 scroll)
        {
            if (itemsReference == null) return;
            int key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(itemsReference);
            key = key ^ (rows << 8) ^ (columns << 16);
            InventoryScroll[key] = scroll;
        }

        /// <summary>
        /// Programmatically scroll the grid identified by gridId so the given cell (row, column) is brought into view.
        /// This sets the stored scroll target; the actual clamped scroll will be applied during the next call to InventoryGrid(...).
        /// </summary>
        public static void ScrollToCell(string gridId, int row, int column, float cellSize, float cellPadding)
        {
            if (string.IsNullOrEmpty(gridId)) return;
            int key = gridId.GetHashCode();
            // Desired scroll position places the cell at the top-left of the view.
            float x = column * (cellSize + cellPadding);
            float y = row * (cellSize + cellPadding);
            InventoryScroll[key] = new Vector2(x, y);
        }

        /// <summary>
        /// Programmatically scroll the grid identified by an items reference and dimensions so the given cell is visible.
        /// </summary>
        public static void ScrollToCellForItems(object itemsReference, int rows, int columns, int row, int column, float cellSize, float cellPadding)
        {
            if (itemsReference == null) return;
            int key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(itemsReference);
            key = key ^ (rows << 8) ^ (columns << 16);
            float x = column * (cellSize + cellPadding);
            float y = row * (cellSize + cellPadding);
            InventoryScroll[key] = new Vector2(x, y);
        }
    }
}
