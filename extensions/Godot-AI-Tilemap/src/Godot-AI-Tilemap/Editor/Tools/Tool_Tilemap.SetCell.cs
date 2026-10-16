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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    public partial class Tool_Tilemap
    {
        /// <summary>
        /// Editor-only tool — sets a single cell on an existing <c>TileMapLayer</c> to a tile (TileSet source +
        /// atlas coordinates). Arguments are validated pure-managed (source &gt;= 0, atlas coords &gt;= 0) before
        /// any Godot call. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            SetCellToolId,
            Title = "Tilemap / Set Cell"
        )]
        [AiSkillDescription("Set one cell on a TileMapLayer to a tile, addressed by map coords (x,y) and the " +
            "TileSet source id + atlas coords. Returns the layer's updated config.")]
        [AiSkillBody("Place a tile in a single cell of a `TileMapLayer`.\n\n" +
            "## Inputs\n\n" +
            "- `nodePath` — required node path (relative to the edited scene root) of the TileMapLayer.\n" +
            "- `x`, `y` — required cell coordinates on the map grid.\n" +
            "- `sourceId` — required TileSet source id (>= 0; the source must exist in the assigned TileSet to render).\n" +
            "- `atlasX`, `atlasY` — required atlas coordinates of the tile within that source (>= 0).\n\n" +
            "## Behavior\n\n" +
            "Validates the source id (>= 0) and atlas coords (>= 0), then calls `TileMapLayer.SetCell(coords, " +
            "sourceId, atlasCoords)` on the editor main thread, marks the scene unsaved, and returns the layer's " +
            "updated config (including its used cells). Assign a TileSet first with `tilemap-set-tileset`.")]
        [Description("Set a single cell on an existing TileMapLayer, addressed by 'nodePath' (relative to the " +
            "edited scene root). The cell at ('x','y') is set to the tile at atlas ('atlasX','atlasY') of TileSet " +
            "source 'sourceId' (sourceId >= 0, atlas coords >= 0). Returns the layer's updated config.")]
        public TileMapLayerInfo SetCell
        (
            [Description("Node path (relative to the edited scene root) of the TileMapLayer.")]
            string nodePath,
            [Description("Cell X coordinate on the map grid.")]
            int x,
            [Description("Cell Y coordinate on the map grid.")]
            int y,
            [Description("TileSet source id of the tile to place (>= 0).")]
            int sourceId,
            [Description("Atlas X coordinate of the tile within its source (>= 0).")]
            int atlasX,
            [Description("Atlas Y coordinate of the tile within its source (>= 0).")]
            int atlasY
        )
        {
            TilemapValidation.ValidateSourceId(sourceId);
            TilemapValidation.ValidateAtlasCoords(atlasX, atlasY);

            return MainThread.Instance.Run(() =>
            {
                var layer = ResolveTileMapLayerOrThrow(nodePath);

                layer.SetCell(new Vector2I(x, y), sourceId, new Vector2I(atlasX, atlasY));

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(layer);
            });
        }
    }
}
#endif
