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
using Rql;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// One track of an animation, as a read-only row of <c>Animation.tracks</c>: a snapshot with no Godot type, so it is
    /// built by the adapter and unit-testable. Change a track with the Animation's calls (<c>InsertKey</c>,
    /// <c>InsertTransformKey</c>) or by Godot property name (<c>props["tracks/0/path"]</c>).
    /// </summary>
    [RqlDescription("Animation track (read-only): index kind path keyCount")]
    public sealed class AnimationTrackRow
    {
        public AnimationTrackRow(int index, string kind, string path, int keyCount)
        {
            this.index = index;
            this.kind = kind;
            this.path = path;
            this.keyCount = keyCount;
        }

        [RqlDescription("0-based track index (the track argument of InsertKey / InsertTransformKey)")]
        public int index { get; }

        [RqlDescription("value, position-3d, rotation-3d, scale-3d, or Godot's track type name for other kinds")]
        public string kind { get; }

        [RqlDescription("animated path: \"Node:property\" for a value track, \"Node\" for a transform track")]
        public string path { get; }

        [RqlDescription("number of keys on the track")]
        public int keyCount { get; }
    }
}
#endif
