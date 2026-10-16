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

namespace com.IvanMurzak.Godot.MCP.Particles
{
    /// <summary>
    /// Which built-in Godot particle node a tool targets: a 2D <c>GpuParticles2D</c> or a 3D
    /// <c>GpuParticles3D</c>. Pure-managed (no Godot native types) so the parse logic is CI-unit-testable.
    /// </summary>
    public enum ParticlesDimension
    {
        /// <summary><c>GpuParticles2D</c> — a 2D particle emitter.</summary>
        Two,

        /// <summary><c>GpuParticles3D</c> — a 3D particle emitter.</summary>
        Three
    }

    /// <summary>Pure-managed helpers for <see cref="ParticlesDimension"/> parsing and display.</summary>
    public static class ParticlesDimensions
    {
        /// <summary>The Godot class name for a dimension (<c>"GpuParticles2D"</c> / <c>"GpuParticles3D"</c>).</summary>
        public static string ToGodotClassName(this ParticlesDimension dimension) =>
            dimension == ParticlesDimension.Two ? "GpuParticles2D" : "GpuParticles3D";

        /// <summary>The short display label for a dimension (<c>"2D"</c> / <c>"3D"</c>).</summary>
        public static string ToLabel(this ParticlesDimension dimension) =>
            dimension == ParticlesDimension.Two ? "2D" : "3D";

        /// <summary>
        /// Parse a user/LLM-supplied dimension string into a <see cref="ParticlesDimension"/>. Accepts (case-
        /// and whitespace-insensitive): <c>"2D"</c>, <c>"2"</c>, <c>"two"</c> → <see cref="ParticlesDimension.Two"/>;
        /// <c>"3D"</c>, <c>"3"</c>, <c>"three"</c> → <see cref="ParticlesDimension.Three"/>. Returns false for
        /// anything else (and for null/empty).
        /// </summary>
        public static bool TryParse(string? value, out ParticlesDimension dimension)
        {
            dimension = ParticlesDimension.Three;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value!.Trim().ToLowerInvariant())
            {
                case "2d":
                case "2":
                case "two":
                    dimension = ParticlesDimension.Two;
                    return true;
                case "3d":
                case "3":
                case "three":
                    dimension = ParticlesDimension.Three;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Parse a dimension string, throwing <see cref="ArgumentException"/> on an unrecognized value. Used
        /// by tools that require an explicit, valid dimension.
        /// </summary>
        public static ParticlesDimension Parse(string? value)
        {
            if (TryParse(value, out var dimension))
                return dimension;
            throw new ArgumentException(
                $"Unrecognized particles dimension '{value}'. Use '2D' or '3D'.", nameof(value));
        }
    }
}
