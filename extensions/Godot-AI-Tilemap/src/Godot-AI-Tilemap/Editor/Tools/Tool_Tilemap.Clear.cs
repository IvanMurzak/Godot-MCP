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
        /// Editor-only tool — clears ALL cells on an existing <c>TileMapLayer</c> (leaves the TileSet assigned).
        /// Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            ClearToolId,
            Title = "Tilemap / Clear"
        )]
        [AiSkillDescription("Remove all cells from a TileMapLayer (the assigned TileSet is kept). Returns the " +
            "now-empty layer's config.")]
        [AiSkillBody("Erase every cell on a `TileMapLayer`.\n\n" +
            "## Inputs\n\n" +
            "- `nodePath` — required node path (relative to the edited scene root) of the TileMapLayer.\n\n" +
            "## Behavior\n\n" +
            "Calls `TileMapLayer.Clear()` on the editor main thread (removes all placed cells; the assigned " +
            "TileSet resource is NOT removed), marks the scene unsaved, and returns the layer's config (now with " +
            "`UsedCellCount` 0 and an empty cell list).")]
        [Description("Clear ALL cells on an existing TileMapLayer, addressed by 'nodePath' (relative to the " +
            "edited scene root). Removes every placed cell; the assigned TileSet is kept. Returns the layer's " +
            "updated (empty) config.")]
        public TileMapLayerInfo Clear
        (
            [Description("Node path (relative to the edited scene root) of the TileMapLayer to clear.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var layer = ResolveTileMapLayerOrThrow(nodePath);

                layer.Clear();

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(layer);
            });
        }
    }
}
#endif
