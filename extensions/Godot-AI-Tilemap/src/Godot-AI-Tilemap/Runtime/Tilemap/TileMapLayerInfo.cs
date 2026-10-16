/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// Pure-managed, serializable snapshot of a Godot <c>TileMapLayer</c> node's scalar configuration plus its
    /// used cells — the structured result every <c>tilemap-*</c> tool returns. Holds ONLY primitives, strings,
    /// and a list of <see cref="TileCell"/> (no Godot native types), so it is safe to build inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate and return across the tool boundary, it serializes cleanly
    /// through ReflectorNet, and a snapshot can be constructed with no Godot binary (CI-unit-testable).
    /// </summary>
    public sealed class TileMapLayerInfo
    {
        /// <summary>Scene path of the node (empty for an unbound snapshot).</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>The node's Godot class name (e.g. <c>"TileMapLayer"</c>).</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Resource path of the assigned TileSet (<c>res://…</c>), or empty when none is assigned.</summary>
        public string TileSetPath { get; set; } = string.Empty;

        /// <summary>Number of used (non-empty) cells on the layer.</summary>
        public int UsedCellCount { get; set; }

        /// <summary>The layer's used cells (empty when the layer has no tiles).</summary>
        public List<TileCell> UsedCells { get; set; } = new();
    }
}
