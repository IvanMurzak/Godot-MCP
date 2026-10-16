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
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for two things shared by the
    /// editor-driving GridMap tools and the pure-managed <c>gridmap-defaults</c> tool:
    /// <list type="number">
    ///   <item>a recommended starter configuration (a unit-cube grid), and</item>
    ///   <item>the value-clamping rules the editor tools apply before writing to a live node (so an LLM can
    ///         never push a node into an invalid state — a non-positive cell size, a negative item id, or an
    ///         out-of-range orthogonal orientation).</item>
    /// </list>
    /// Keeping this logic pure-managed means it is verified by fast xUnit tests with no Godot binary, and the
    /// editor tools simply reuse it.
    /// </summary>
    public static class GridMapDefaults
    {
        /// <summary>The recommended starter cell size (a unit cube) for a new GridMap.</summary>
        public const double DefaultCellSize = 1.0;

        /// <summary>The smallest cell-size dimension the clamp will yield (a tiny positive value, never &lt;= 0).</summary>
        public const double MinCellSize = 0.01;

        /// <summary>The recommended starter <c>cell_scale</c> for a new GridMap.</summary>
        public const double DefaultCellScale = 1.0;

        /// <summary>The recommended starter <c>cell_octant_size</c> for a new GridMap (Godot's own default).</summary>
        public const int DefaultCellOctantSize = 8;

        /// <summary>Godot's <c>GridMap.INVALID_CELL_ITEM</c> (-1) — the item id that denotes an empty cell.</summary>
        public const int InvalidItem = -1;

        /// <summary>The lowest valid orthogonal orientation index.</summary>
        public const int MinOrientation = 0;

        /// <summary>The highest valid orthogonal orientation index (24 orthogonal rotations: 0..23).</summary>
        public const int MaxOrientation = 23;

        /// <summary>
        /// A recommended starter configuration as a fully-populated <see cref="GridMapNodeInfo"/> (no bound
        /// node, so <see cref="GridMapNodeInfo.NodePath"/> / <see cref="GridMapNodeInfo.TypeName"/> are empty).
        /// The <c>gridmap-defaults</c> tool returns this.
        /// </summary>
        public static GridMapNodeInfo For() => new()
        {
            NodePath = string.Empty,
            Kind = "GridMap",
            TypeName = string.Empty,
            CellSizeX = DefaultCellSize,
            CellSizeY = DefaultCellSize,
            CellSizeZ = DefaultCellSize,
            CellScale = DefaultCellScale,
            CellOctantSize = DefaultCellOctantSize,
            CellCenterX = true,
            CellCenterY = true,
            CellCenterZ = true,
            MeshLibraryPath = string.Empty,
            CellCount = 0
        };

        /// <summary>Clamp a cell-size dimension to a strictly-positive value (Godot requires &gt; 0).</summary>
        public static double ClampCellSize(double value) =>
            (value <= 0.0 || double.IsNaN(value)) ? MinCellSize : value;

        /// <summary>
        /// Clamp a MeshLibrary item id to a non-negative value. <c>gridmap-set-cell</c> places items, so a
        /// negative id (which Godot treats as "clear") is floored to 0; clearing is the separate
        /// <c>gridmap-clear-cell</c> tool's job.
        /// </summary>
        public static int ClampItem(int item) => item < 0 ? 0 : item;

        /// <summary>Clamp an orthogonal orientation index into the valid 0..23 range.</summary>
        public static int ClampOrientation(int orientation)
        {
            if (orientation < MinOrientation) return MinOrientation;
            if (orientation > MaxOrientation) return MaxOrientation;
            return orientation;
        }
    }
}
