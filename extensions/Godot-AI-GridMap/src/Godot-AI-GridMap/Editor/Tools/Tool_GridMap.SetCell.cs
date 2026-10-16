/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    public partial class Tool_GridMap
    {
        /// <summary>
        /// Editor-only tool — sets a single cell of a <c>GridMap</c> to a MeshLibrary item, with an optional
        /// orthogonal orientation. The item id is clamped to &gt;= 0 (clearing is <c>gridmap-clear-cell</c>) and
        /// the orientation into 0..23. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            SetCellToolId,
            Title = "GridMap / Set Cell"
        )]
        [Description("Set a single cell of a GridMap (addressed by 'nodePath', relative to the edited scene " +
            "root) at grid coordinates ('cellX','cellY','cellZ') to MeshLibrary item 'item' (clamped to >= 0). " +
            "Optionally pass 'orientation', an orthogonal orientation index 0..23 (clamped). To clear a cell " +
            "instead, use gridmap-clear-cell. Returns the resulting cell (coordinates, item, orientation).")]
        public GridMapCellInfo SetCell
        (
            [Description("Node path (relative to the edited scene root) of the GridMap node.")]
            string nodePath,
            [Description("Cell X coordinate (grid space).")]
            int cellX,
            [Description("Cell Y coordinate (grid space).")]
            int cellY,
            [Description("Cell Z coordinate (grid space).")]
            int cellZ,
            [Description("MeshLibrary item id to place in the cell; clamped to >= 0.")]
            int item,
            [Description("Optional orthogonal orientation index (0..23); clamped into range. Defaults to 0.")]
            int orientation = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var grid = ResolveGridMapOrThrow(nodePath);
                var clampedItem = GridMapDefaults.ClampItem(item);
                var clampedOrientation = GridMapDefaults.ClampOrientation(orientation);

                grid.SetCellItem(new Vector3I(cellX, cellY, cellZ), clampedItem, clampedOrientation);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadCell(grid, cellX, cellY, cellZ);
            });
        }
    }
}
#endif
