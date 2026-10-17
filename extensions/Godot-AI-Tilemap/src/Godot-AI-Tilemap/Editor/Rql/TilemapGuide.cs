/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Linq;
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// The agent guide of the Tilemap RQL module, rendered into the <c>godot-rql</c> skill (<see cref="TilemapModule"/>).
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e
    /// manifest (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene whose root is a Node2D.
    /// </summary>
    public static class TilemapGuide
    {
        public const string Topic = "Tilemap";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "root | call AddChild(\"TileMapLayer\", \"Layer\")   a tile layer (one per layer; replaces the deprecated TileMap)\n" +
            "nodes | where name == \"Layer\" | set TileSet = \"res://e2e-tileset.tres\"   its TileSet (sources and their atlas tiles)\n" +
            "nodes | where name == \"Layer\" | call SetCell(0, 0, 0, 0, 0)   cell (0, 0) = source 0, atlas tile (0, 0)\n" +
            "nodes | where name == \"Layer\" | call SetCell(1, 0, 0, 2, 1, 1)   cell (1, 0) = atlas tile (2, 1), alternative 1\n" +
            "nodes | where name == \"Layer\" | get usedCells | select x y sourceId atlasX atlasY alternative   read the painted cells\n" +
            "nodes | where name == \"Layer\" | call EraseCell(0, 0)   empty one cell\n" +
            "nodes | where $ is TileMapLayer | select name usedCellCount TileSet   read the layers\n" +
            "nodes | where name == \"Layer\" | call Clear()   erase every cell (the TileSet stays)";

        public const string Markdown =
            "A TileMapLayer (Godot 4.3+) paints the tiles of a TileSet on a 2D grid. Add one with `AddChild`, assign the " +
            "TileSet and paint cells (undoable; the scene is marked unsaved):\n\n```\n" + Examples + "\n```\n\n" +
            "A tile is addressed by its TileSet source id and its atlas coordinates in that source (plus an alternative tile " +
            "id, 0 = the base tile); `TileSet.props[\"…\"]` reads the TileSet's sources. Source ids, atlas coordinates and " +
            "alternatives are validated (0 or more; erase with `EraseCell`): a rejected value changes nothing. Other settings " +
            "are C# members (`Enabled`, `YSortEnabled`, `CollisionEnabled`, `NavigationEnabled` …) or `props[\"…\"]`; " +
            "`rql-schema \"TileMapLayer\"` lists them.";

        /// <summary>The queries of <see cref="Examples"/>, in order.</summary>
        public static string[] Queries =>
            Examples.Split('\n').Select(l => Regex.Split(l.Trim(), @"\s{3,}")[0]).Where(l => l.Length > 0).ToArray();
    }
}
