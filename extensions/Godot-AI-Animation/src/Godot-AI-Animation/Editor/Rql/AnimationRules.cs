/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Globalization;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// The value rules the module enforces before an animation, library, track or key is written: what Godot itself rejects
    /// (it logs an error and ignores the call) plus what the replaced 0.x tools refused. Each returns the rejection reason, or
    /// null when the value is valid. The adapters run them as RQL <c>validate</c> callbacks (members) or before a call changes
    /// anything (methods throw the reason), so a rejected value changes nothing. Pure-managed (no Godot API), so it is
    /// unit-tested.
    /// </summary>
    public static class AnimationRules
    {
        /// <summary>Godot's smallest animation length (<c>ANIM_MIN_LENGTH</c>; <c>set_length</c> refuses less).</summary>
        public const double MinLength = 0.001;

        /// <summary>Animation length in seconds: at least <see cref="MinLength"/> and finite.</summary>
        public static string? Length(double length) =>
            length >= MinLength && !double.IsInfinity(length)
                ? null
                : $"Length must be a number of seconds of at least {MinLength.ToString(CultureInfo.InvariantCulture)} (got {Format(length)})";

        /// <summary>A key time in seconds: zero or more and finite.</summary>
        public static string? KeyTime(double time) =>
            time >= 0 && !double.IsInfinity(time)
                ? null
                : $"key time must be a number of seconds of at least 0 (got {Format(time)})";

        /// <summary>
        /// An animation name inside a library: not empty and without '/', ':', ',' or '[' (Godot's
        /// <c>AnimationLibrary::is_valid_animation_name</c>; '/' separates the library in a qualified name).
        /// </summary>
        public static string? AnimationName(string? name) =>
            string.IsNullOrEmpty(name) ? "an animation name cannot be empty"
            : name!.IndexOfAny(Forbidden) >= 0 ? $"animation name '{name}' cannot contain '/', ':', ',' or '['"
            : null;

        /// <summary>A library name: may be empty (the global library) but without '/', ':', ',' or '[' (Godot's <c>is_valid_library_name</c>).</summary>
        public static string? LibraryName(string? name) =>
            name == null ? "a library name cannot be null (\"\" is the global library)"
            : name.IndexOfAny(Forbidden) >= 0 ? $"library name '{name}' cannot contain '/', ':', ',' or '['"
            : null;

        /// <summary>A track index of an animation with <paramref name="trackCount"/> tracks.</summary>
        public static string? TrackIndex(int index, int trackCount) =>
            index >= 0 && index < trackCount
                ? null
                : $"track {index} does not exist (the animation has {trackCount} track(s): 0 to {trackCount - 1})";

        /// <summary>
        /// The path of a new track: not empty; a value track animates a property, so its path is <c>"node:property"</c>
        /// (e.g. <c>"Mover:position:x"</c>) — the 0.x tool required the property too.
        /// </summary>
        public static string? TrackPath(AnimationTrackKind kind, string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "a track path cannot be empty (\"Node\" or \"Node:property\")";
            if (kind == AnimationTrackKind.Value && path!.IndexOf(':') < 0)
                return $"a value track animates a property: give the path as \"node:property\" (e.g. \"{path}:position:x\"), got '{path}'";
            return null;
        }

        private static readonly char[] Forbidden = { '/', ':', ',', '[' };

        private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
