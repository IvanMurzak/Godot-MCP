/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// The agent guide of the Terrain3D RQL module, rendered into the <c>godot-rql</c> skill (<see cref="Terrain3DModule"/>).
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e manifest
    /// (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene whose root is a Node3D, with the
    /// Terrain3D addon installed.
    /// </summary>
    public static class Terrain3DGuide
    {
        public const string Topic = "Terrain3D";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "terrain3d.addon | select installed hint   is the Terrain3D addon loaded (and how to install it)\n" +
            "root | call AddChild(\"Terrain3D\", \"Terrain\")   a new terrain (the node's C# type is Node3D; class is \"Terrain3D\")\n" +
            "terrain3d | select name regionSize dataDirectory meshLods meshSize vertexSpacing version   the terrains of the edited scene\n" +
            "terrain3d | where name == \"Terrain\" | call ChangeRegionSize(1024)   re-slice into 1024-wide regions (64 … 2048; not undoable)\n" +
            "terrain3d | where name == \"Terrain\" | get node | set props[\"data_directory\"] = \"res://terrain_data\"   where the region files are saved\n" +
            "terrain3d | where name == \"Terrain\" | get node | set props[\"material\"] = terrain3d.newMaterial()   a fresh Terrain3DMaterial\n" +
            "terrain3d | where name == \"Terrain\" | get node | set props[\"mesh_lods\"] = 8, props[\"vertex_spacing\"] = 2   any Terrain3D property by its snake_case name\n" +
            "terrain3d | select name regionSize dataDirectory material.class regionCount meshLods vertexSpacing   read them back";

        public const string Markdown =
            "Terrain3D (the TokisanGames GDExtension addon) is a node class without a C# type: its nodes are Node3D in RQL, " +
            "`class == \"Terrain3D\"`. The `terrain3d` source lists the edited scene's terrains as rows with friendly members and " +
            "the node itself (`node`); write the terrain's properties on that node, by their snake_case names (`get node | set " +
            "props[\"…\"] = …`): those writes are undoable and mark the scene unsaved. `rql-schema \"Terrain3D\"` lists the " +
            "properties.\n\n```\n" + Examples + "\n```\n\n" +
            "`ChangeRegionSize(size)` accepts 64, 128, 256, 512, 1024 or 2048 (anything else fails and changes nothing); it re-slices " +
            "the region data and is not undoable (the size lives in the region files, not in the scene). Set `data_directory` to a " +
            "writable res:// folder before sculpting: Terrain3D saves its regions there when the scene is saved (`editor | call " +
            "SaveScene()`). Typical values: region size 256 (small) to 1024 (large worlds), `mesh_lods` 7, `mesh_size` 48, " +
            "`vertex_spacing` 1. Without the addon, `terrain3d` is empty and `terrain3d.addon` says how to install it.";
    }
}
