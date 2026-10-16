/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    public partial class Tool_GridMap
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API, so it lives OUTSIDE <c>#if TOOLS</c> and is fully
        /// CI-unit-testable (see <c>Tool_GridMap_DefaultsTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool gridmap-defaults</c>. Returns the recommended starter configuration for a new
        /// GridMap (a unit-cube grid), which the LLM can then pass to <c>gridmap-create</c>.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "GridMap / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter configuration (cell size, cell scale, octant size, " +
            "centering) for a new Godot GridMap node. Pure-managed: touches no scene, so it is safe to call " +
            "any time to discover sane defaults before creating a real GridMap. Optionally pass 'cellSize' to " +
            "override the uniform cell-size dimension (clamped to > 0); when omitted a unit cube (1) is used.")]
        public GridMapNodeInfo Defaults
        (
            [Description("Optional uniform cell-size dimension to apply to X/Y/Z; clamped to > 0. " +
                "When null, the default cell size (1) is returned.")]
            double? cellSize = null
        )
        {
            var info = GridMapDefaults.For();
            if (cellSize.HasValue)
            {
                var clamped = GridMapDefaults.ClampCellSize(cellSize.Value);
                info.CellSizeX = clamped;
                info.CellSizeY = clamped;
                info.CellSizeZ = clamped;
            }
            return info;
        }
    }
}
