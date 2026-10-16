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
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for two things shared by the
    /// editor-driving animation tools and the pure-managed <c>animation-defaults</c> tool:
    /// <list type="number">
    ///   <item>a recommended starter configuration for a new <c>Animation</c> resource, and</item>
    ///   <item>the value-clamping rules the editor tools apply before writing to a live resource (so an LLM
    ///         can never push an animation into an invalid state — non-positive length, negative keyframe
    ///         time, negative track index).</item>
    /// </list>
    /// Keeping this logic pure-managed means it is verified by fast xUnit tests with no Godot binary, and the
    /// editor tools simply reuse it.
    /// </summary>
    public static class AnimationDefaults
    {
        /// <summary>The recommended starter <c>length</c> (seconds) for a new animation.</summary>
        public const double DefaultLength = 1.0;

        /// <summary>The smallest length the clamp will yield (a tiny positive value, never 0).</summary>
        public const double MinLength = 0.01;

        /// <summary>The default library name (the empty string = the player's global/default library).</summary>
        public const string DefaultLibraryName = "";

        /// <summary>The recommended starter loop mode for a new animation.</summary>
        public const AnimationLoopMode DefaultLoopMode = AnimationLoopMode.None;

        /// <summary>
        /// A recommended starter configuration as a fully-populated <see cref="AnimationClipInfo"/> (no bound
        /// resource, so <see cref="AnimationClipInfo.Name"/> / <see cref="AnimationClipInfo.QualifiedName"/> are
        /// empty and no tracks). The <c>animation-defaults</c> tool returns this.
        /// </summary>
        public static AnimationClipInfo For(AnimationLoopMode loopMode) => new()
        {
            Name = string.Empty,
            Library = DefaultLibraryName,
            QualifiedName = string.Empty,
            Length = DefaultLength,
            LoopMode = loopMode.ToLabel(),
            TrackCount = 0,
            Tracks = new()
        };

        /// <summary>Clamp <c>length</c> to a strictly-positive number of seconds.</summary>
        public static double ClampLength(double length) =>
            (length <= 0.0 || double.IsNaN(length)) ? MinLength : length;

        /// <summary>Clamp a keyframe <c>time</c> to a non-negative number of seconds.</summary>
        public static double ClampTime(double time) =>
            (time < 0.0 || double.IsNaN(time)) ? 0.0 : time;

        /// <summary>Clamp a track index to a non-negative value.</summary>
        public static int ClampTrackIndex(int trackIndex) => trackIndex < 0 ? 0 : trackIndex;
    }
}
