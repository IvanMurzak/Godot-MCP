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

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// TileMapLayer (Godot 4.3+): read-only <c>usedCells</c> rows and <c>usedCellCount</c>; calls
    /// <c>SetCell(x, y, sourceId, atlasX, atlasY, alternative)</c>, <c>EraseCell(x, y)</c>, <c>Clear()</c>. Undoable: the cells
    /// are the layer's own stored data.
    /// </summary>
    public sealed class Godot_TileMapLayer_Adapter : RqlTypeAdapter<TileMapLayer>
    {
        public override string Description =>
            "TileMapLayer (Tilemap extension): TileSet, usedCells (x y sourceId atlasX atlasY alternative), usedCellCount; " +
            "call SetCell(x, y, sourceId, atlasX, atlasY, alternative), EraseCell(x, y), Clear()";

        protected override void Members(MemberTable<TileMapLayer> m)
        {
            m.Add<List<TilemapCellRow>>("usedCells", UsedCells, description: "the painted cells (read-only rows: x y sourceId atlasX atlasY alternative)")
             .Add<int>("usedCellCount", l => l.GetUsedCells().Count, description: "number of painted cells");

            m.Untyped
                .AddMethod("SetCell", new[]
                    {
                        new CallableParameter("x", typeof(int)), new CallableParameter("y", typeof(int)), new CallableParameter("sourceId", typeof(int)),
                        new CallableParameter("atlasX", typeof(int)), new CallableParameter("atlasY", typeof(int)),
                        new CallableParameter("alternative", typeof(int), hasDefault: true, defaultValue: 0),
                    }, typeof(void),
                    (o, a) => { SetCell((TileMapLayer)o, (int)a[0]!, (int)a[1]!, (int)a[2]!, (int)a[3]!, (int)a[4]!, (int)a[5]!); return null; },
                    "paint cell (x, y) with the tile at atlas (atlasX, atlasY) of TileSet source sourceId (alternative tile, default 0)");
            m.Method<int, int>("EraseCell", (l, x, y) => l.EraseCell(new Vector2I(x, y)), "x", "y", "empty the cell (x, y)");
            m.Expose("Clear", "erase every cell (the TileSet stays)");
        }

        private static void SetCell(TileMapLayer layer, int x, int y, int sourceId, int atlasX, int atlasY, int alternative)
        {
            Require(TilemapRules.SourceId(sourceId));
            Require(TilemapRules.AtlasCoords(atlasX, atlasY));
            Require(TilemapRules.Alternative(alternative));
            layer.SetCell(new Vector2I(x, y), sourceId, new Vector2I(atlasX, atlasY), alternative);
        }

        private static List<TilemapCellRow> UsedCells(TileMapLayer layer) =>
            layer.GetUsedCells()
                .Select(c =>
                {
                    var atlas = layer.GetCellAtlasCoords(c);
                    return new TilemapCellRow(c.X, c.Y, layer.GetCellSourceId(c), atlas.X, atlas.Y, layer.GetCellAlternativeTile(c));
                })
                .ToList();

        private static void Require(string? reason)
        {
            if (reason != null) throw new ArgumentException(reason);
        }
    }
}
#endif
