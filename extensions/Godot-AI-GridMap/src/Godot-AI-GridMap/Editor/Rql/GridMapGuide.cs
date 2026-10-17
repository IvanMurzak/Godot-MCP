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
    /// The agent guide of the GridMap RQL module, rendered into the <c>godot-rql</c> skill (<see cref="GridMapModule"/>).
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e
    /// manifest (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene whose root is a Node3D.
    /// </summary>
    public static class GridMapGuide
    {
        public const string Topic = "GridMap";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "root | call AddChild(\"GridMap\", \"Grid\")   a GridMap node\n" +
            "nodes | where name == \"Grid\" | set MeshLibrary = \"res://e2e-meshlib.tres\", CellSize = \"(1, 1, 1)\"   its MeshLibrary (the item ids) and cell size (validated)\n" +
            "nodes | where name == \"Grid\" | call SetCell(1, 0, 1, 0)   place item 0 at cell (1, 0, 1)\n" +
            "nodes | where name == \"Grid\" | call SetCell(2, 0, 1, 0, 10)   with an orthogonal orientation (0 to 23)\n" +
            "nodes | where name == \"Grid\" | get cells | select x y z item orientation   read the used cells\n" +
            "nodes | where name == \"Grid\" | call ClearCell(1, 0, 1)   empty one cell\n" +
            "nodes | where $ is GridMap | select name cellCount MeshLibrary   read the grids\n" +
            "nodes | where name == \"Grid\" | call Clear()   remove every cell (the MeshLibrary stays)";

        public const string Markdown =
            "A GridMap places the items of a MeshLibrary on a 3D grid. Add one with `AddChild`, assign the library and paint " +
            "cells (undoable; the scene is marked unsaved):\n\n```\n" + Examples + "\n```\n\n" +
            "Cells are integer grid coordinates (from -32767 to 32767); `item` is a MeshLibrary item id (0 or more; " +
            "`MeshLibrary.props[\"item/0/name\"]` or the `itemName` column of `cells` names it), `orientation` an orthogonal " +
            "orientation index (0 = none). `CellSize` (at least 0.001 per axis, default \"(2, 2, 2)\"), " +
            "`CellOctantSize` (1 or more, default 8) and the cell arguments are validated: a rejected value changes nothing. " +
            "Other settings are C# members (`CellScale`, `CellCenterX` …, `CollisionLayer`) or `props[\"…\"]`; " +
            "`rql-schema \"GridMap\"` lists them.";
    }
}
