/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using Rql;

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    /// <summary>
    /// One used cell of a GridMap, as a read-only row of <c>GridMap.cells</c>: a snapshot with no Godot type, built by the
    /// adapter. Change cells with the GridMap's calls (<c>SetCell</c>, <c>ClearCell</c>, <c>Clear</c>).
    /// </summary>
    [RqlDescription("GridMap cell (read-only): x y z item itemName orientation")]
    public sealed class GridMapCellRow
    {
        public GridMapCellRow(int x, int y, int z, int item, string? itemName, int orientation)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.item = item;
            this.itemName = itemName;
            this.orientation = orientation;
        }

        [RqlDescription("cell X (grid coordinates)")]
        public int x { get; }

        [RqlDescription("cell Y (grid coordinates)")]
        public int y { get; }

        [RqlDescription("cell Z (grid coordinates)")]
        public int z { get; }

        [RqlDescription("MeshLibrary item id")]
        public int item { get; }

        [RqlDescription("the item's name in the MeshLibrary, or null when the library has no such item")]
        public string? itemName { get; }

        [RqlDescription("orthogonal orientation index (0 to 23)")]
        public int orientation { get; }
    }
}
