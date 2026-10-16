/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// How a Godot <c>Animation</c> resource loops, mirroring Godot's <c>Animation.LoopModeEnum</c>
    /// (<c>None</c>=0, <c>Linear</c>=1, <c>Pingpong</c>=2). Pure-managed (no Godot native types) so the
    /// parse/format logic is CI-unit-testable with no Godot binary.
    /// </summary>
    public enum AnimationLoopMode
    {
        /// <summary>Play once and stop (Godot <c>Animation.LoopModeEnum.None</c> = 0).</summary>
        None,

        /// <summary>Loop from the start each time (Godot <c>Animation.LoopModeEnum.Linear</c> = 1).</summary>
        Linear,

        /// <summary>Loop back and forth (Godot <c>Animation.LoopModeEnum.Pingpong</c> = 2).</summary>
        PingPong
    }

    /// <summary>Pure-managed helpers for <see cref="AnimationLoopMode"/> parsing, display and Godot mapping.</summary>
    public static class AnimationLoopModes
    {
        /// <summary>The integer that maps to Godot's <c>Animation.LoopModeEnum</c> (0 / 1 / 2).</summary>
        public static int ToGodotValue(this AnimationLoopMode mode) => mode switch
        {
            AnimationLoopMode.None => 0,
            AnimationLoopMode.Linear => 1,
            AnimationLoopMode.PingPong => 2,
            _ => 0
        };

        /// <summary>Map a Godot <c>Animation.LoopModeEnum</c> integer back to <see cref="AnimationLoopMode"/>.</summary>
        public static AnimationLoopMode FromGodotValue(int value) => value switch
        {
            1 => AnimationLoopMode.Linear,
            2 => AnimationLoopMode.PingPong,
            _ => AnimationLoopMode.None
        };

        /// <summary>The short display label (<c>"none"</c> / <c>"linear"</c> / <c>"pingpong"</c>).</summary>
        public static string ToLabel(this AnimationLoopMode mode) => mode switch
        {
            AnimationLoopMode.Linear => "linear",
            AnimationLoopMode.PingPong => "pingpong",
            _ => "none"
        };

        /// <summary>
        /// Parse a user/LLM-supplied loop-mode string. Accepts (case- and whitespace-insensitive):
        /// <c>"none"/"once"/"0"</c> → <see cref="AnimationLoopMode.None"/>;
        /// <c>"linear"/"loop"/"1"</c> → <see cref="AnimationLoopMode.Linear"/>;
        /// <c>"pingpong"/"ping-pong"/"ping pong"/"2"</c> → <see cref="AnimationLoopMode.PingPong"/>.
        /// Returns false for anything else (and for null/empty).
        /// </summary>
        public static bool TryParse(string? value, out AnimationLoopMode mode)
        {
            mode = AnimationLoopMode.None;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value!.Trim().ToLowerInvariant())
            {
                case "none":
                case "once":
                case "0":
                    mode = AnimationLoopMode.None;
                    return true;
                case "linear":
                case "loop":
                case "1":
                    mode = AnimationLoopMode.Linear;
                    return true;
                case "pingpong":
                case "ping-pong":
                case "ping pong":
                case "pong":
                case "2":
                    mode = AnimationLoopMode.PingPong;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Parse a loop-mode string, throwing <see cref="ArgumentException"/> on an unrecognized value. Used
        /// by tools that require an explicit, valid loop mode.
        /// </summary>
        public static AnimationLoopMode Parse(string? value)
        {
            if (TryParse(value, out var mode))
                return mode;
            throw new ArgumentException(
                $"Unrecognized animation loop mode '{value}'. Use 'none', 'linear' or 'pingpong'.", nameof(value));
        }
    }
}
