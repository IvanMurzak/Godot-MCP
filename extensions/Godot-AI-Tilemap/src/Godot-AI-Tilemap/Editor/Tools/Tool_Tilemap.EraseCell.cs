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
        /// Editor-only tool — erases a single cell on an existing <c>TileMapLayer</c> (sets it back to empty).
        /// Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            EraseCellToolId,
            Title = "Tilemap / Erase Cell"
        )]
        [AiSkillDescription("Erase one cell on a TileMapLayer (set it back to empty), addressed by map coords " +
            "(x,y). Returns the layer's updated config.")]
        [AiSkillBody("Clear a single cell of a `TileMapLayer`.\n\n" +
            "## Inputs\n\n" +
            "- `nodePath` — required node path (relative to the edited scene root) of the TileMapLayer.\n" +
            "- `x`, `y` — required cell coordinates on the map grid.\n\n" +
            "## Behavior\n\n" +
            "Calls `TileMapLayer.EraseCell(coords)` on the editor main thread (equivalent to setting the cell's " +
            "source to -1), marks the scene unsaved, and returns the layer's updated config. Erasing an already-" +
            "empty cell is a no-op.")]
        [Description("Erase a single cell on an existing TileMapLayer, addressed by 'nodePath' (relative to the " +
            "edited scene root). The cell at ('x','y') is set back to empty. Returns the layer's updated config.")]
        public TileMapLayerInfo EraseCell
        (
            [Description("Node path (relative to the edited scene root) of the TileMapLayer.")]
            string nodePath,
            [Description("Cell X coordinate on the map grid.")]
            int x,
            [Description("Cell Y coordinate on the map grid.")]
            int y
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var layer = ResolveTileMapLayerOrThrow(nodePath);

                layer.EraseCell(new Vector2I(x, y));

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(layer);
            });
        }
    }
}
#endif
