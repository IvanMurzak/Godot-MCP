/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    public partial class Tool_Tilemap
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even though every tilemap-* tool is editor-only — so a single source of truth is pinned
        // by the unit tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Editor tool id — create a TileMapLayer node (<c>tilemap-create</c>).</summary>
        public const string CreateToolId = "tilemap-create";

        /// <summary>Editor tool id — assign a TileSet resource to a layer (<c>tilemap-set-tileset</c>).</summary>
        public const string SetTileSetToolId = "tilemap-set-tileset";

        /// <summary>Editor tool id — set a single cell (<c>tilemap-set-cell</c>).</summary>
        public const string SetCellToolId = "tilemap-set-cell";

        /// <summary>Editor tool id — erase a single cell (<c>tilemap-erase-cell</c>).</summary>
        public const string EraseCellToolId = "tilemap-erase-cell";

        /// <summary>Editor tool id — list a layer's used cells (<c>tilemap-get-used-cells</c>).</summary>
        public const string GetUsedCellsToolId = "tilemap-get-used-cells";

        /// <summary>Editor tool id — clear all cells on a layer (<c>tilemap-clear</c>).</summary>
        public const string ClearToolId = "tilemap-clear";
    }
}
