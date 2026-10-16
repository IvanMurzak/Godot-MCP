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
    /// Pure-managed, serializable snapshot of a single Godot <c>Animation</c> resource's configuration — the
    /// structured result the <c>animation-create</c> / <c>-add-track</c> / <c>-insert-key</c> / <c>-defaults</c>
    /// tools return. Holds ONLY primitives + strings + a list of <see cref="AnimationTrackInfo"/> (no Godot
    /// native types), so it is safe to build inside a <c>MainThread.Instance.Run(...)</c> delegate and return
    /// across the tool boundary, it serializes cleanly through ReflectorNet, and the pure-managed defaults
    /// helper can produce one with no Godot binary (CI-unit-testable).
    /// </summary>
    public sealed class AnimationClipInfo
    {
        /// <summary>Animation name within its library (empty for a defaults snapshot not bound to a library).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Name of the owning <c>AnimationLibrary</c> (empty string = the global/default library).</summary>
        public string Library { get; set; } = string.Empty;

        /// <summary>
        /// The fully-qualified name an <c>AnimationPlayer</c> addresses the clip by: <c>"library/name"</c> for a
        /// named library, or just <c>"name"</c> for the global library. Empty for a defaults snapshot.
        /// </summary>
        public string QualifiedName { get; set; } = string.Empty;

        /// <summary>Animation length in seconds (Godot <c>length</c>; always &gt; 0).</summary>
        public double Length { get; set; }

        /// <summary>Loop-mode label: <c>"none"</c> / <c>"linear"</c> / <c>"pingpong"</c> (Godot <c>loop_mode</c>).</summary>
        public string LoopMode { get; set; } = string.Empty;

        /// <summary>Number of tracks in the animation.</summary>
        public int TrackCount { get; set; }

        /// <summary>Per-track snapshots (empty for a defaults snapshot).</summary>
        public List<AnimationTrackInfo> Tracks { get; set; } = new();
    }
}
