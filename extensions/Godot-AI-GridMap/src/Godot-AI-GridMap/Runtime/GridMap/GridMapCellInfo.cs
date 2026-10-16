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
    /// <summary>
    /// Pure-managed, serializable snapshot of a single <c>GridMap</c> cell — the structured result the
    /// cell-level tools (<c>gridmap-set-cell</c>, <c>gridmap-clear-cell</c>) return. Holds ONLY primitives, so
    /// it is safe to build inside a <c>MainThread.Instance.Run(...)</c> delegate and serializes cleanly through
    /// ReflectorNet.
    ///
    /// <para>
    /// An <see cref="Item"/> of <see cref="GridMapDefaults.InvalidItem"/> (-1, Godot's <c>INVALID_CELL_ITEM</c>)
    /// denotes an empty cell — exposed as the pure-managed, unit-testable <see cref="IsEmpty"/> invariant.
    /// </para>
    /// </summary>
    public sealed class GridMapCellInfo
    {
        /// <summary>Scene path of the owning GridMap node.</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>Cell X coordinate (grid space).</summary>
        public int CellX { get; set; }

        /// <summary>Cell Y coordinate (grid space).</summary>
        public int CellY { get; set; }

        /// <summary>Cell Z coordinate (grid space).</summary>
        public int CellZ { get; set; }

        /// <summary>The MeshLibrary item id stored in the cell; <see cref="GridMapDefaults.InvalidItem"/> when empty.</summary>
        public int Item { get; set; }

        /// <summary>The cell's orthogonal orientation index (0..23); -1 when the cell is empty.</summary>
        public int Orientation { get; set; }

        /// <summary>True when the cell is empty (its <see cref="Item"/> is negative). Pure-managed invariant.</summary>
        public bool IsEmpty => Item < 0;

        /// <summary>Build a cell-info representing an EMPTY cell at the given coordinates.</summary>
        public static GridMapCellInfo Empty(string nodePath, int x, int y, int z) => new()
        {
            NodePath = nodePath,
            CellX = x,
            CellY = y,
            CellZ = z,
            Item = GridMapDefaults.InvalidItem,
            Orientation = GridMapDefaults.InvalidItem
        };
    }
}
