/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// Pure-managed, serializable snapshot of a Godot <c>AnimationPlayer</c> node — the structured result the
    /// <c>animation-player-create</c> / <c>-library-add</c> / <c>-get</c> tools return. Holds ONLY primitives +
    /// strings + lists (no Godot native types), so it is safe to build inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate and serializes cleanly through ReflectorNet.
    /// </summary>
    public sealed class AnimationPlayerInfo
    {
        /// <summary>Scene path of the <c>AnimationPlayer</c> node.</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>The node's Godot class name (<c>"AnimationPlayer"</c>).</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Names of the animation libraries on the player (the global library is the empty string).</summary>
        public List<string> Libraries { get; set; } = new();

        /// <summary>Number of animation libraries on the player.</summary>
        public int LibraryCount { get; set; }

        /// <summary>Fully-qualified names of every animation on the player (<c>"library/name"</c> or <c>"name"</c>).</summary>
        public List<string> Animations { get; set; } = new();

        /// <summary>Number of animations on the player.</summary>
        public int AnimationCount { get; set; }

        /// <summary>
        /// The config of a single requested animation, populated only when <c>animation-get</c> is called with
        /// an explicit animation name; otherwise <c>null</c>.
        /// </summary>
        public AnimationClipInfo? SelectedAnimation { get; set; }
    }
}
