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
        /// Editor-only tool — creates a <c>TileMapLayer</c> node in the currently edited scene and returns its
        /// structured config. All Godot API access is marshalled onto the editor main thread via
        /// <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            CreateToolId,
            Title = "Tilemap / Create"
        )]
        [AiSkillDescription("Create a Godot TileMapLayer node (4.3+, the modern replacement for TileMap) in the " +
            "currently edited scene, optionally renamed and parented to a chosen node. Returns the new layer's " +
            "structured config.")]
        [AiSkillBody("Create a `TileMapLayer` node in the edited scene and return its structured config.\n\n" +
            "## Inputs\n\n" +
            "- `name` — optional node name; when omitted Godot's default name for the type is used.\n" +
            "- `parentPath` — optional node path (relative to the edited scene root) to parent the new layer " +
            "under; when omitted the layer is parented to the scene root.\n\n" +
            "## Behavior\n\n" +
            "Adds a new `TileMapLayer` under the resolved parent, sets its owner to the scene root (so it is " +
            "saved with the scene), marks the scene unsaved, selects it in the editor, and returns its config " +
            "(node path, type name, assigned TileSet path, used-cell count). The layer starts with no TileSet " +
            "and no cells — call `tilemap-set-tileset` then `tilemap-set-cell` to populate it.")]
        [Description("Create a TileMapLayer node in the currently edited Godot scene and return its structured " +
            "config. Optionally pass 'name' to rename it and 'parentPath' (a node path relative to the scene " +
            "root) to parent it (defaults to the scene root). The new node's owner is set to the scene root so " +
            "it is saved with the scene.")]
        public TileMapLayerInfo Create
        (
            [Description("Name for the new TileMapLayer node. When omitted, Godot's default name is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var root = GetEditedSceneRootOrThrow();

                Node parent = root;
                if (!string.IsNullOrWhiteSpace(parentPath))
                    parent = root.GetNodeOrNull(new NodePath(parentPath))
                        ?? throw new ArgumentException(
                            $"No parent node found at path '{parentPath}'.", nameof(parentPath));

                var node = new TileMapLayer();
                if (!string.IsNullOrWhiteSpace(name))
                    node.Name = name;

                parent.AddChild(node);
                node.Owner = root; // so the node is persisted when the scene is saved

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(node);

                return ReadInfo(node);
            });
        }
    }
}
#endif
