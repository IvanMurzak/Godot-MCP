/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    public partial class Tool_GridMap
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>gridmap-defaults</c>).</summary>
        public const string DefaultsToolId = "gridmap-defaults";

        /// <summary>Editor tool id — create a GridMap node (<c>gridmap-create</c>).</summary>
        public const string CreateToolId = "gridmap-create";

        /// <summary>Editor tool id — assign a MeshLibrary resource (<c>gridmap-set-mesh-library</c>).</summary>
        public const string SetMeshLibraryToolId = "gridmap-set-mesh-library";

        /// <summary>Editor tool id — set a single cell to an item (<c>gridmap-set-cell</c>).</summary>
        public const string SetCellToolId = "gridmap-set-cell";

        /// <summary>Editor tool id — clear a single cell (<c>gridmap-clear-cell</c>).</summary>
        public const string ClearCellToolId = "gridmap-clear-cell";

        /// <summary>Editor tool id — clear all cells (<c>gridmap-clear</c>).</summary>
        public const string ClearToolId = "gridmap-clear";

        /// <summary>Editor tool id — read a GridMap's config (<c>gridmap-get</c>).</summary>
        public const string GetToolId = "gridmap-get";
    }
}
