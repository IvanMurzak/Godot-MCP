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
    /// The kind of track <c>Animation.AddTrack(kind, path)</c> creates, mapping onto the subset of Godot's
    /// <c>Animation.TrackType</c> the module supports: a generic <c>Value</c> track (<c>"node:property"</c>)
    /// plus the three dedicated 3D transform tracks (<c>Position3D</c> / <c>Rotation3D</c> / <c>Scale3D</c>).
    /// Pure-managed (no Godot native types) so the parse/map logic is CI-unit-testable.
    /// </summary>
    public enum AnimationTrackKind
    {
        /// <summary>A generic value track — animates a node sub-property (Godot <c>TrackType.Value</c> = 0).</summary>
        Value,

        /// <summary>A 3D position track (Godot <c>TrackType.Position3D</c> = 1).</summary>
        Position3D,

        /// <summary>A 3D rotation track (Godot <c>TrackType.Rotation3D</c> = 2).</summary>
        Rotation3D,

        /// <summary>A 3D scale track (Godot <c>TrackType.Scale3D</c> = 3).</summary>
        Scale3D
    }

    /// <summary>Pure-managed helpers for <see cref="AnimationTrackKind"/> parsing, display and Godot mapping.</summary>
    public static class AnimationTrackKinds
    {
        /// <summary>The integer that maps to Godot's <c>Animation.TrackType</c> (0 / 1 / 2 / 3).</summary>
        public static int ToGodotTrackType(this AnimationTrackKind kind) => kind switch
        {
            AnimationTrackKind.Value => 0,
            AnimationTrackKind.Position3D => 1,
            AnimationTrackKind.Rotation3D => 2,
            AnimationTrackKind.Scale3D => 3,
            _ => 0
        };

        /// <summary>
        /// Map a Godot <c>Animation.TrackType</c> integer back to <see cref="AnimationTrackKind"/>, or
        /// <c>null</c> for track types the module does not model (method/bezier/audio/blend-shape/animation).
        /// </summary>
        public static AnimationTrackKind? FromGodotTrackType(int trackType) => trackType switch
        {
            0 => AnimationTrackKind.Value,
            1 => AnimationTrackKind.Position3D,
            2 => AnimationTrackKind.Rotation3D,
            3 => AnimationTrackKind.Scale3D,
            _ => null
        };

        /// <summary>Whether this kind is one of the dedicated 3D transform tracks (not a generic value track).</summary>
        public static bool IsTransform3D(this AnimationTrackKind kind) =>
            kind != AnimationTrackKind.Value;

        /// <summary>The short display label (<c>"value"</c> / <c>"position-3d"</c> / <c>"rotation-3d"</c> / <c>"scale-3d"</c>).</summary>
        public static string ToLabel(this AnimationTrackKind kind) => kind switch
        {
            AnimationTrackKind.Position3D => "position-3d",
            AnimationTrackKind.Rotation3D => "rotation-3d",
            AnimationTrackKind.Scale3D => "scale-3d",
            _ => "value"
        };

        /// <summary>
        /// Parse a user/LLM-supplied track-kind string (case-, whitespace- and separator-insensitive). Accepts
        /// <c>"value"/"val"</c>; <c>"position-3d"/"position3d"/"pos3d"/"position"/"pos"</c>;
        /// <c>"rotation-3d"/"rotation3d"/"rot3d"/"rotation"/"rot"</c>;
        /// <c>"scale-3d"/"scale3d"/"scale"</c>. Returns false for anything else (and for null/empty).
        /// </summary>
        public static bool TryParse(string? value, out AnimationTrackKind kind)
        {
            kind = AnimationTrackKind.Value;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Normalize: lower-case and strip '-', '_' and spaces so '3d', '3-d', '3_d' all collapse.
            var n = value!.Trim().ToLowerInvariant()
                .Replace("-", "").Replace("_", "").Replace(" ", "");

            switch (n)
            {
                case "value":
                case "val":
                    kind = AnimationTrackKind.Value;
                    return true;
                case "position3d":
                case "pos3d":
                case "position":
                case "pos":
                    kind = AnimationTrackKind.Position3D;
                    return true;
                case "rotation3d":
                case "rot3d":
                case "rotation":
                case "rot":
                    kind = AnimationTrackKind.Rotation3D;
                    return true;
                case "scale3d":
                case "scale":
                    kind = AnimationTrackKind.Scale3D;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Parse a track-kind string, throwing <see cref="ArgumentException"/> on an unrecognized value. Used
        /// by the module (the kind argument of AddTrack).
        /// </summary>
        public static AnimationTrackKind Parse(string? value)
        {
            if (TryParse(value, out var kind))
                return kind;
            throw new ArgumentException(
                $"Unrecognized animation track kind '{value}'. Use 'value', 'position-3d', " +
                "'rotation-3d' or 'scale-3d'.", nameof(value));
        }
    }
}
