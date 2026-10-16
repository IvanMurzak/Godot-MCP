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
        /// Editor-only tool — adds a new (empty) named <c>AnimationLibrary</c> to an existing
        /// <c>AnimationPlayer</c>. Animations created later can target this library (qualified as
        /// <c>"library/name"</c>). Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            LibraryAddToolId,
            Title = "Animation / Library Add"
        )]
        [Description("Add a new empty AnimationLibrary to an existing AnimationPlayer, addressed by 'nodePath' " +
            "(relative to the edited scene root). 'libraryName' is the (non-empty) library name; animations " +
            "added to it are addressed as 'libraryName/animationName'. Fails if a library with that name " +
            "already exists. Returns the player's updated config.")]
        public AnimationPlayerInfo LibraryAdd
        (
            [Description("Node path (relative to the edited scene root) of the AnimationPlayer.")]
            string nodePath,
            [Description("Name for the new animation library (must be non-empty and not already present).")]
            string libraryName
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var player = ResolveAnimationPlayerOrThrow(nodePath);

                if (string.IsNullOrWhiteSpace(libraryName))
                    throw new ArgumentException(
                        "A non-empty library name is required.", nameof(libraryName));

                if (player.HasAnimationLibrary(libraryName))
                    throw new ArgumentException(
                        $"An animation library named '{libraryName}' already exists on the player.",
                        nameof(libraryName));

                var err = player.AddAnimationLibrary(libraryName, new AnimationLibrary());
                if (err != Error.Ok)
                    throw new InvalidOperationException(
                        $"Failed to add animation library '{libraryName}' ({err}).");

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadPlayerInfo(player);
            });
        }
    }
}
#endif
