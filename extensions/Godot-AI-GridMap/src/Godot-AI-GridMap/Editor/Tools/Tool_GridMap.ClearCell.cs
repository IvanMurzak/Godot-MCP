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
        /// Editor-only tool — clears a single cell of a <c>GridMap</c> (sets it empty via Godot's
        /// <c>INVALID_CELL_ITEM</c>). Main-thread-marshalled. Returns the now-empty cell.
        /// </summary>
        [AiTool
        (
            ClearCellToolId,
            Title = "GridMap / Clear Cell"
        )]
        [Description("Clear a single cell of a GridMap (addressed by 'nodePath', relative to the edited scene " +
            "root) at grid coordinates ('cellX','cellY','cellZ') — i.e. set it empty. Returns the resulting " +
            "cell, whose item is -1 (empty). To clear the whole grid, use gridmap-clear.")]
        public GridMapCellInfo ClearCell
        (
            [Description("Node path (relative to the edited scene root) of the GridMap node.")]
            string nodePath,
            [Description("Cell X coordinate (grid space).")]
            int cellX,
            [Description("Cell Y coordinate (grid space).")]
            int cellY,
            [Description("Cell Z coordinate (grid space).")]
            int cellZ
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var grid = ResolveGridMapOrThrow(nodePath);
                grid.SetCellItem(new Vector3I(cellX, cellY, cellZ), GridMapDefaults.InvalidItem);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadCell(grid, cellX, cellY, cellZ);
            });
        }
    }
}
#endif
