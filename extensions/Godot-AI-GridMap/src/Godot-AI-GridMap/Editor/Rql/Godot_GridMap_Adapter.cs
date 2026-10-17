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
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rql.Execution;
// The root namespace ends in '.GridMap', which shadows the type 'Godot.GridMap': alias it.
using GdGridMap = Godot.GridMap;

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    /// <summary>
    /// GridMap: validated <c>CellSize</c> / <c>CellOctantSize</c>, read-only <c>cells</c> rows and <c>cellCount</c>; calls
    /// <c>SetCell(x, y, z, item, orientation)</c>, <c>ClearCell(x, y, z)</c>, <c>Clear()</c>. Undoable: the cells are the
    /// GridMap's own stored data.
    /// </summary>
    public sealed class Godot_GridMap_Adapter : RqlTypeAdapter<GdGridMap>
    {
        public override string Description =>
            "GridMap (GridMap extension): MeshLibrary, CellSize (validated), cells (x y z item itemName orientation), cellCount; " +
            "call SetCell(x, y, z, item, orientation), ClearCell(x, y, z), Clear()";

        protected override void Members(MemberTable<GdGridMap> m)
        {
            m.Add<Vector3>("CellSize", g => g.CellSize, (g, v) => g.CellSize = v,
                    validate: (g, v) => GridMapRules.CellSize(v.X, v.Y, v.Z), description: "cell edge lengths \"(x, y, z)\" (at least 0.001 each)")
             .Add<int>("CellOctantSize", g => g.CellOctantSize, (g, v) => g.CellOctantSize = v,
                    validate: (g, v) => GridMapRules.OctantSize(v), description: "cells per rendering octant edge (at least 1)")
             .Add<List<GridMapCellRow>>("cells", Cells, description: "the used cells (read-only rows: x y z item itemName orientation)")
             .Add<int>("cellCount", g => g.GetUsedCells().Count, description: "number of used cells");

            m.Untyped
                .AddMethod("SetCell", new[]
                    {
                        new CallableParameter("x", typeof(int)), new CallableParameter("y", typeof(int)), new CallableParameter("z", typeof(int)),
                        new CallableParameter("item", typeof(int)), new CallableParameter("orientation", typeof(int), hasDefault: true, defaultValue: 0),
                    }, typeof(void),
                    (o, a) => { SetCell((GdGridMap)o, (int)a[0]!, (int)a[1]!, (int)a[2]!, (int)a[3]!, (int)a[4]!); return null; },
                    "place MeshLibrary item (id >= 0) at cell (x, y, z) with an orthogonal orientation 0 to 23 (default 0)")
                .AddMethod("ClearCell", new[] { new CallableParameter("x", typeof(int)), new CallableParameter("y", typeof(int)), new CallableParameter("z", typeof(int)) },
                    typeof(void), (o, a) => { ClearCell((GdGridMap)o, (int)a[0]!, (int)a[1]!, (int)a[2]!); return null; },
                    "empty the cell (x, y, z)");
            m.Expose("Clear", "remove every cell (the MeshLibrary and settings stay)");
        }

        private static void SetCell(GdGridMap grid, int x, int y, int z, int item, int orientation)
        {
            Require(GridMapRules.Cell(x, y, z));
            Require(GridMapRules.Item(item));
            Require(GridMapRules.Orientation(orientation));
            grid.SetCellItem(new Vector3I(x, y, z), item, orientation);
        }

        private static void ClearCell(GdGridMap grid, int x, int y, int z)
        {
            Require(GridMapRules.Cell(x, y, z));
            grid.SetCellItem(new Vector3I(x, y, z), (int)GdGridMap.InvalidCellItem);
        }

        private static List<GridMapCellRow> Cells(GdGridMap grid)
        {
            var library = grid.MeshLibrary;
            var items = library != null ? new HashSet<int>(library.GetItemList().Select(i => (int)i)) : new HashSet<int>();
            return grid.GetUsedCells()
                .Select(c =>
                {
                    var item = grid.GetCellItem(c);
                    return new GridMapCellRow(c.X, c.Y, c.Z, item, items.Contains(item) ? library!.GetItemName(item) : null, grid.GetCellItemOrientation(c));
                })
                .ToList();
        }

        private static void Require(string? reason)
        {
            if (reason != null) throw new ArgumentException(reason);
        }
    }
}
#endif
