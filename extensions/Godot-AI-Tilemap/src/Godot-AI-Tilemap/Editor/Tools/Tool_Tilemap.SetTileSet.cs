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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    public partial class Tool_Tilemap
    {
        /// <summary>
        /// Editor-only tool — assigns a <c>TileSet</c> resource (loaded from a <c>res://</c> path) to an existing
        /// <c>TileMapLayer</c>. The path is validated pure-managed before any Godot call. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            SetTileSetToolId,
            Title = "Tilemap / Set TileSet"
        )]
        [AiSkillDescription("Assign a TileSet resource (by res:// path) to an existing TileMapLayer so its cells " +
            "can render. Returns the layer's updated config.")]
        [AiSkillBody("Load a `TileSet` resource and assign it to a `TileMapLayer`.\n\n" +
            "## Inputs\n\n" +
            "- `nodePath` — required node path (relative to the edited scene root) of the TileMapLayer.\n" +
            "- `tileSetPath` — required `res://` path to a `.tres` TileSet resource.\n\n" +
            "## Behavior\n\n" +
            "Validates that `tileSetPath` is a `res://` path, loads it as a `TileSet`, assigns it to the layer, " +
            "marks the scene unsaved, and returns the layer's updated config. Errors clearly when the path is " +
            "not a resource path or does not resolve to a TileSet.")]
        [Description("Assign a TileSet resource to an existing TileMapLayer, addressed by 'nodePath' (relative " +
            "to the edited scene root). 'tileSetPath' is a Godot resource path (res://…) to a .tres TileSet. " +
            "Returns the layer's updated config.")]
        public TileMapLayerInfo SetTileSet
        (
            [Description("Node path (relative to the edited scene root) of the TileMapLayer to configure.")]
            string nodePath,
            [Description("Godot resource path (res://…) to the TileSet (.tres) resource to assign.")]
            string tileSetPath
        )
        {
            var resourcePath = TilemapValidation.ValidateResourcePath(tileSetPath);

            return MainThread.Instance.Run(() =>
            {
                var layer = ResolveTileMapLayerOrThrow(nodePath);

                var tileSet = ResourceLoader.Load<TileSet>(resourcePath);
                if (tileSet == null)
                    throw new ArgumentException(
                        $"No TileSet resource could be loaded from '{resourcePath}'.", nameof(tileSetPath));

                layer.TileSet = tileSet;

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(layer);
            });
        }
    }
}
#endif
