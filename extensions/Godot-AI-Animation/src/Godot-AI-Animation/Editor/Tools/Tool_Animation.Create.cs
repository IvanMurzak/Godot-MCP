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
// '.Animation' namespace shadows 'Godot.Animation'; alias the Godot type so it is unambiguous.
using GodotAnimation = Godot.Animation;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    public partial class Tool_Animation
    {
        /// <summary>
        /// Editor-only tool — creates a new <c>Animation</c> resource inside an <c>AnimationPlayer</c>'s
        /// library and returns its structured config. The target library is auto-created when missing (so the
        /// global/default library needs no prior <c>animation-library-add</c>). <c>length</c> and
        /// <c>loopMode</c> are clamped/parsed through the pure-managed defaults. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            CreateToolId,
            Title = "Animation / Create"
        )]
        [Description("Create a new Animation in an AnimationPlayer (addressed by 'nodePath', relative to the " +
            "edited scene root) and return its config. 'name' is the animation name. Optional 'libraryName' " +
            "selects/creates the owning library (default '' = the global library; the clip is then addressed " +
            "as 'name', or 'libraryName/name' for a named library). Optional 'length' (seconds, clamped > 0, " +
            "default 1.0) and 'loopMode' ('none'/'linear'/'pingpong', default 'none').")]
        public AnimationClipInfo Create
        (
            [Description("Node path (relative to the edited scene root) of the AnimationPlayer.")]
            string nodePath,
            [Description("Name for the new animation (within its library).")]
            string name,
            [Description("Owning library name. Defaults to '' (the global/default library); auto-created if missing.")]
            string? libraryName = null,
            [Description("Animation length in seconds (Godot 'length'); clamped to > 0. Defaults to 1.0.")]
            double? length = null,
            [Description("Loop mode: 'none', 'linear' or 'pingpong'. Defaults to 'none'.")]
            string? loopMode = null
        )
        {
            var mode = string.IsNullOrWhiteSpace(loopMode)
                ? AnimationDefaults.DefaultLoopMode
                : AnimationLoopModes.Parse(loopMode);

            return MainThread.Instance.Run(() =>
            {
                var player = ResolveAnimationPlayerOrThrow(nodePath);

                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("An animation name is required.", nameof(name));

                var lib = libraryName ?? AnimationDefaults.DefaultLibraryName;
                var library = GetOrAddLibrary(player, lib);

                if (library.HasAnimation(name))
                    throw new ArgumentException(
                        $"An animation named '{name}' already exists in library '{lib}'.", nameof(name));

                var animation = new GodotAnimation
                {
                    Length = (float)AnimationDefaults.ClampLength(length ?? AnimationDefaults.DefaultLength),
                    LoopMode = (GodotAnimation.LoopModeEnum)mode.ToGodotValue()
                };

                var err = library.AddAnimation(name, animation);
                if (err != Error.Ok)
                    throw new InvalidOperationException(
                        $"Failed to add animation '{name}' to library '{lib}' ({err}).");

                EditorInterface.Singleton.MarkSceneAsUnsaved();

                var qualified = string.IsNullOrEmpty(lib) ? name : $"{lib}/{name}";
                return ReadClipInfo(qualified, animation);
            });
        }
    }
}
#endif
