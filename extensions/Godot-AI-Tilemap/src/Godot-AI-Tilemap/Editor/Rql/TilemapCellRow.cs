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
using Rql;

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// One painted cell of a TileMapLayer, as a read-only row of <c>TileMapLayer.usedCells</c>: a snapshot with no Godot type,
    /// built by the adapter. Change cells with the layer's calls (<c>SetCell</c>, <c>EraseCell</c>, <c>Clear</c>).
    /// </summary>
    [RqlDescription("TileMapLayer cell (read-only): x y sourceId atlasX atlasY alternative")]
    public sealed class TilemapCellRow
    {
        public TilemapCellRow(int x, int y, int sourceId, int atlasX, int atlasY, int alternative)
        {
            this.x = x;
            this.y = y;
            this.sourceId = sourceId;
            this.atlasX = atlasX;
            this.atlasY = atlasY;
            this.alternative = alternative;
        }

        [RqlDescription("cell X (map coordinates)")]
        public int x { get; }

        [RqlDescription("cell Y (map coordinates)")]
        public int y { get; }

        [RqlDescription("TileSet source id")]
        public int sourceId { get; }

        [RqlDescription("atlas X of the tile in its source")]
        public int atlasX { get; }

        [RqlDescription("atlas Y of the tile in its source")]
        public int atlasY { get; }

        [RqlDescription("alternative tile id (0 = the base tile)")]
        public int alternative { get; }
    }
}
#endif
