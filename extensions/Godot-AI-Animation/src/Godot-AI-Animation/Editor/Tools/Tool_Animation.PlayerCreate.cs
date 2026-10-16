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

namespace com.IvanMurzak.Godot.MCP.Animation
{
    public partial class Tool_Animation
    {
        /// <summary>
        /// Editor-only tool — creates an <c>AnimationPlayer</c> node in the currently edited scene and returns
        /// its structured config. All Godot API access is marshalled onto the editor main thread via
        /// <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            PlayerCreateToolId,
            Title = "Animation / Player Create"
        )]
        [Description("Create an AnimationPlayer node in the currently edited Godot scene and return its " +
            "structured config (node path, libraries, animation list). Optionally pass 'parentPath' (a node " +
            "path relative to the scene root) to parent it (defaults to the scene root) and 'name' to rename " +
            "it. The new node's owner is set to the scene root so it is saved with the scene.")]
        public AnimationPlayerInfo PlayerCreate
        (
            [Description("Name for the new AnimationPlayer node. When omitted, Godot's default name is used.")]
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

                var player = new AnimationPlayer();
                if (!string.IsNullOrWhiteSpace(name))
                    player.Name = name;

                parent.AddChild(player);
                player.Owner = root; // so the node is persisted when the scene is saved

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(player);

                return ReadPlayerInfo(player);
            });
        }
    }
}
#endif
