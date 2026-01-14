using Common.Editor.UI;
using TScript;
using UnityEditor;
using UnityEngine;

namespace Common.Editor
{
    [CustomEditor(typeof(NewBehaviourScript))]
    public class NewBehaviourScriptEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var script = (NewBehaviourScript)target;

            EditorGUILayout.LabelField("IMGUI Drag & Drop Demo", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // --- Single draggable + droppable zone demo ---
            EditorGUILayout.LabelField("Single Slot", EditorStyles.boldLabel);

            // Draggable source
            script.sourceItem = ImGuiDraggableControls.DraggableObjectField(
                "Source Item",
                script.sourceItem,
                allowSceneObjects: false,
                dragTag: "Item");

            // Combined draggable + droppable slot
            ImGuiDropResult<Sprite> slotDrop;
            (script.slotItem, slotDrop) = ImGuiDropControls.DraggableDropZone(
                label: "Slot",
                currentValue: script.slotItem,
                allowSceneObjects: false,
                dragTag: "Item",
                requiredTag: "Item",
                height: 40f,
                customDrawer: (rect, res) =>
                {
                    // Draw preview inside the slot (will draw placeholder if null)
                    PreviewObjectDrawer.DrawObjectPreview(rect, script.slotItem);

                    // Draw a simple frame on top
                    Color border = res.CanAccept ? Color.green : Color.gray;
                    Handles.DrawSolidRectangleWithOutline(
                        new[]
                        {
                            new Vector3(rect.xMin, rect.yMin),
                            new Vector3(rect.xMax, rect.yMin),
                            new Vector3(rect.xMax, rect.yMax),
                            new Vector3(rect.xMin, rect.yMax)
                        },
                        Color.clear,
                        border);

                    if (script.slotItem == null)
                    {
                        GUI.Label(rect, "Empty Slot", EditorStyles.centeredGreyMiniLabel);
                    }
                });

            if (slotDrop.Performed)
            {
                EditorUtility.SetDirty(script);
            }

            EditorGUILayout.Space();

            // --- Inventory grid demo ---
            EditorGUILayout.LabelField("Inventory Grid", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            script.rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", script.rows));
            script.columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", script.columns));
            if (EditorGUI.EndChangeCheck())
            {
                script.SyncFlatArraySize();
            }

            int rows = Mathf.Max(1, script.rows);
            int cols = Mathf.Max(1, script.columns);
            int count = rows * cols;

            if (script.flatItems == null || script.flatItems.Length != count)
            {
                script.SyncFlatArraySize();
            }

            // Convert flat array to 2D view
            Sprite[,] grid = new Sprite[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    grid[r, c] = script.flatItems![idx];
                }
            }

            var changed = InventoryGridControl.InventoryGrid(
                items: grid,
                rows: rows,
                columns: cols,
                cellSize: 100f,
                cellPadding: 4f,
                allowSceneObjects: false,
                dragTag: "Item",
                requiredTag: "Item",
                customDrawer: (rect, res, index) =>
                {
                    // Draw the sprite preview for this cell (or placeholder)
                    PreviewObjectDrawer.DrawObjectPreview(rect, grid[index.row, index.column]);

                    Color border = res.CanAccept ? Color.green : Color.gray;
                    Handles.DrawSolidRectangleWithOutline(
                        new[]
                        {
                            new Vector3(rect.xMin, rect.yMin),
                            new Vector3(rect.xMax, rect.yMin),
                            new Vector3(rect.xMax, rect.yMax),
                            new Vector3(rect.xMin, rect.yMax)
                        },
                        Color.clear,
                        border);

                    if (grid[index.row, index.column] == null)
                    {
                        GUI.Label(rect, $"{index.row},{index.column}", EditorStyles.centeredGreyMiniLabel);
                    }
                },
                gridId: $"InventoryGrid_{script.GetInstanceID()}",
                maxVisibleHeight: 240f,
                allowHorizontalScroll: true
            );

            // Copy 2D back to flat array if anything changed visually
            var gridChanged = false;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var idx = r * cols + c;
                    if (script.flatItems![idx] == grid[r, c]) continue;
                    script.flatItems[idx] = grid[r, c];
                    gridChanged = true;
                }
            }

            if (gridChanged || changed.row >= 0)
            {
                EditorUtility.SetDirty(script);
            }
        }
    }
}