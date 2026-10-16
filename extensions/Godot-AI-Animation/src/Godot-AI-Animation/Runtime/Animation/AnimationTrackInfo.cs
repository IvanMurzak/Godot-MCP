/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// Pure-managed, serializable snapshot of a single track inside a Godot <c>Animation</c> resource. Holds
    /// ONLY primitives + strings (no Godot native types), so it is safe to build inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate, serializes cleanly through ReflectorNet, and the
    /// pure-managed defaults helper can build one with no Godot binary (CI-unit-testable).
    /// </summary>
    public sealed class AnimationTrackInfo
    {
        /// <summary>Track index within the animation (0-based).</summary>
        public int Index { get; set; }

        /// <summary>Track kind label: <c>"value"</c> / <c>"position-3d"</c> / <c>"rotation-3d"</c> / <c>"scale-3d"</c>
        /// (or Godot's raw track-type name for a kind these tools do not model).</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The track path (for a value track, <c>"node:property"</c>; for a transform track, the node path).</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Number of keyframes currently on the track.</summary>
        public int KeyCount { get; set; }
    }
}
