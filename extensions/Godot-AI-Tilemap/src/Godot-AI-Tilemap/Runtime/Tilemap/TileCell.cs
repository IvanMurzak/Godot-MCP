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
    /// <summary>
    /// Pure-managed, serializable snapshot of one placed cell on a Godot <c>TileMapLayer</c> — the structured
    /// element <c>tilemap-get-used-cells</c> returns (and <c>tilemap-set-cell</c> echoes back). Holds ONLY
    /// primitives (no Godot native types such as <c>Vector2I</c>), so it is safe to build inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate and return across the tool boundary, it serializes cleanly
    /// through ReflectorNet, and it is fully CI-unit-testable with no Godot binary.
    ///
    /// <para>
    /// The fields mirror what a <c>TileMapLayer</c> stores per cell: the map coordinates (<see cref="X"/>,
    /// <see cref="Y"/>), the TileSet source id (<see cref="SourceId"/>), and the atlas coordinates within that
    /// source (<see cref="AtlasX"/>, <see cref="AtlasY"/>). These names are stable across Godot 4.3 … 4.5.
    /// </para>
    /// </summary>
    public sealed class TileCell
    {
        /// <summary>Cell X coordinate on the map grid (Godot cell <c>coords.x</c>).</summary>
        public int X { get; set; }

        /// <summary>Cell Y coordinate on the map grid (Godot cell <c>coords.y</c>).</summary>
        public int Y { get; set; }

        /// <summary>TileSet source id of the tile in this cell (Godot <c>source_id</c>; <c>-1</c> = empty).</summary>
        public int SourceId { get; set; }

        /// <summary>Atlas X coordinate of the tile within its source (Godot <c>atlas_coords.x</c>).</summary>
        public int AtlasX { get; set; }

        /// <summary>Atlas Y coordinate of the tile within its source (Godot <c>atlas_coords.y</c>).</summary>
        public int AtlasY { get; set; }

        /// <summary>Parameterless constructor (ReflectorNet serialization / object initializers).</summary>
        public TileCell() { }

        /// <summary>Construct a fully-populated cell snapshot.</summary>
        public TileCell(int x, int y, int sourceId, int atlasX, int atlasY)
        {
            X = x;
            Y = y;
            SourceId = sourceId;
            AtlasX = atlasX;
            AtlasY = atlasY;
        }

        /// <summary>Human-readable form: <c>(x,y) source=S atlas=(ax,ay)</c>.</summary>
        public override string ToString() =>
            $"({X},{Y}) source={SourceId} atlas=({AtlasX},{AtlasY})";
    }
}
