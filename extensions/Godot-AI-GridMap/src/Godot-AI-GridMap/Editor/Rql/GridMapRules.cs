/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Globalization;

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    /// <summary>
    /// The value rules the module enforces before a GridMap is written: what Godot itself rejects (it logs an error and
    /// ignores the call) plus what the replaced 0.x tools enforced (item ids of at least 0 — erasing is ClearCell —, an
    /// orientation of 0 to 23). Each returns the rejection reason, or null when the value is valid. Pure-managed (no Godot
    /// API), so it is unit-tested.
    /// </summary>
    public static class GridMapRules
    {
        /// <summary>Godot's smallest cell edge (<c>set_cell_size</c> refuses less on any axis).</summary>
        public const float MinCellSize = 0.001f;

        /// <summary>
        /// Cells are addressed within ±32767 on every axis: GridMap stores a cell key as three int16 values, so a larger
        /// coordinate that <c>set_cell_item</c> still accepts (it checks ±(2^20 - 1)) would wrap onto another cell.
        /// </summary>
        public const int MaxCoordinate = short.MaxValue;

        /// <summary>The number of orthogonal orientations (<c>get_orthogonal_index_from_basis</c>: 0 to 23).</summary>
        public const int OrientationCount = 24;

        public static string? CellSize(float x, float y, float z) =>
            x >= MinCellSize && y >= MinCellSize && z >= MinCellSize && !float.IsInfinity(x) && !float.IsInfinity(y) && !float.IsInfinity(z)
                ? null
                : $"CellSize must be at least {F(MinCellSize)} on every axis (got ({F(x)}, {F(y)}, {F(z)}))";

        public static string? OctantSize(int size) =>
            size >= 1 ? null : $"CellOctantSize must be at least 1 (got {size})";

        public static string? Cell(int x, int y, int z) =>
            Math.Abs((long)x) <= MaxCoordinate && Math.Abs((long)y) <= MaxCoordinate && Math.Abs((long)z) <= MaxCoordinate
                ? null
                : $"cell ({x}, {y}, {z}) is outside the grid: coordinates go from -{MaxCoordinate} to {MaxCoordinate}";

        public static string? Item(int item) =>
            item >= 0 ? null : $"item must be a MeshLibrary item id of at least 0 (got {item}); empty a cell with ClearCell(x, y, z)";

        public static string? Orientation(int orientation) =>
            orientation >= 0 && orientation < OrientationCount
                ? null
                : $"orientation must be an orthogonal orientation index from 0 to {OrientationCount - 1} (got {orientation})";

        private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
