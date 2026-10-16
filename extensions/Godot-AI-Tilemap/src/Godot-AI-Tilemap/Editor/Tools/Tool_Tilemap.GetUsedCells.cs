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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    public partial class Tool_Tilemap
    {
        /// <summary>
        /// Editor-only, read-only tool — lists the used (non-empty) cells of an existing <c>TileMapLayer</c>,
        /// each as a pure-managed <see cref="TileCell"/>. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            GetUsedCellsToolId,
            Title = "Tilemap / Get Used Cells",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [AiSkillDescription("List the used (non-empty) cells of a TileMapLayer — each cell's map coords, TileSet " +
            "source id, and atlas coords. Read-only.")]
        [AiSkillBody("Read the used cells of a `TileMapLayer`.\n\n" +
            "## Inputs\n\n" +
            "- `nodePath` — required node path (relative to the edited scene root) of the TileMapLayer.\n\n" +
            "## Behavior\n\n" +
            "Calls `TileMapLayer.GetUsedCells()` on the editor main thread and returns the layer's config with " +
            "every used cell expanded (map coords + source id + atlas coords) and a `UsedCellCount`. Read-only: " +
            "does not modify the scene.")]
        [Description("List the used (non-empty) cells of an existing TileMapLayer, addressed by 'nodePath' " +
            "(relative to the edited scene root). Returns the layer's config including each used cell's map " +
            "coordinates, TileSet source id, and atlas coordinates. Read-only: does not modify the scene.")]
        public TileMapLayerInfo GetUsedCells
        (
            [Description("Node path (relative to the edited scene root) of the TileMapLayer to read.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() => ReadInfo(ResolveTileMapLayerOrThrow(nodePath)));
        }
    }
}
#endif
