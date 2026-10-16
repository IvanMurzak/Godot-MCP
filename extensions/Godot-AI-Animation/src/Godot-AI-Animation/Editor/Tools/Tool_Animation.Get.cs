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

namespace com.IvanMurzak.Godot.MCP.Animation
{
    public partial class Tool_Animation
    {
        /// <summary>
        /// Editor-only, read-only tool — reads an <c>AnimationPlayer</c>'s libraries and animation list, and
        /// (when an <c>animationName</c> is supplied) that animation's full config including its tracks.
        /// Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "Animation / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read an AnimationPlayer's config — its libraries and the qualified names of all its " +
            "animations — addressed by 'nodePath' (relative to the edited scene root). When 'animationName' " +
            "(a qualified clip name) is supplied, also returns that animation's full config (length, loop " +
            "mode, tracks) under 'SelectedAnimation'. Read-only: does not modify the scene.")]
        public AnimationPlayerInfo Get
        (
            [Description("Node path (relative to the edited scene root) of the AnimationPlayer.")]
            string nodePath,
            [Description("Optional qualified animation name to read in detail ('name' or 'library/name'). " +
                "When omitted, only the player's library + animation list is returned.")]
            string? animationName = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var player = ResolveAnimationPlayerOrThrow(nodePath);
                var info = ReadPlayerInfo(player);

                if (!string.IsNullOrWhiteSpace(animationName))
                {
                    var animation = ResolveAnimationOrThrow(player, animationName);
                    info.SelectedAnimation = ReadClipInfo(animationName!, animation);
                }

                return info;
            });
        }
    }
}
#endif
