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
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for two things shared by the
    /// editor-driving particle tools and the pure-managed <c>particles-defaults</c> tool:
    /// <list type="number">
    ///   <item>a recommended starter configuration per <see cref="ParticlesDimension"/>, and</item>
    ///   <item>the value-clamping rules the editor tools apply before writing to a live node (so an LLM can
    ///         never push a node into an invalid state — amount &lt; 1, non-positive lifetime, out-of-range
    ///         ratios, negative speed/preprocess).</item>
    /// </list>
    /// Keeping this logic pure-managed means it is verified by fast xUnit tests with no Godot binary, and the
    /// editor tools simply reuse it.
    /// </summary>
    public static class ParticlesDefaults
    {
        /// <summary>The recommended starter <c>amount</c> for a new emitter.</summary>
        public const int DefaultAmount = 16;

        /// <summary>The recommended starter <c>lifetime</c> (seconds) for a new emitter.</summary>
        public const double DefaultLifetime = 1.0;

        /// <summary>The recommended starter <c>explosiveness</c> (0..1) for a new emitter.</summary>
        public const float DefaultExplosiveness = 0.1f;

        /// <summary>
        /// A recommended starter configuration for the given dimension, as a fully-populated
        /// <see cref="ParticlesNodeInfo"/> (no bound node, so <see cref="ParticlesNodeInfo.NodePath"/> /
        /// <see cref="ParticlesNodeInfo.TypeName"/> are empty). The <c>particles-defaults</c> tool returns this.
        /// </summary>
        public static ParticlesNodeInfo For(ParticlesDimension dimension) => new()
        {
            NodePath = string.Empty,
            Dimension = dimension.ToLabel(),
            TypeName = string.Empty,
            Amount = DefaultAmount,
            Lifetime = DefaultLifetime,
            OneShot = false,
            Emitting = true,
            Explosiveness = DefaultExplosiveness,
            Randomness = 0f,
            SpeedScale = 1.0,
            Preprocess = 0.0,
            LocalCoords = true
        };

        /// <summary>Clamp <c>amount</c> to a valid emitter count (Godot requires &gt;= 1).</summary>
        public static int ClampAmount(int amount) => amount < 1 ? 1 : amount;

        /// <summary>Clamp <c>lifetime</c> to a strictly-positive number of seconds.</summary>
        public static double ClampLifetime(double lifetime) =>
            (lifetime <= 0.0 || double.IsNaN(lifetime)) ? MinLifetime : lifetime;

        /// <summary>The smallest lifetime the clamp will yield (a tiny positive value, never 0).</summary>
        public const double MinLifetime = 0.01;

        /// <summary>Clamp a 0..1 ratio (<c>explosiveness</c> / <c>randomness</c>) into range.</summary>
        public static float ClampRatio(float ratio)
        {
            if (float.IsNaN(ratio)) return 0f;
            if (ratio < 0f) return 0f;
            if (ratio > 1f) return 1f;
            return ratio;
        }

        /// <summary>Clamp <c>speed_scale</c> to a non-negative multiplier.</summary>
        public static double ClampSpeedScale(double speedScale) =>
            (speedScale < 0.0 || double.IsNaN(speedScale)) ? 0.0 : speedScale;

        /// <summary>Clamp <c>preprocess</c> to a non-negative number of seconds.</summary>
        public static double ClampPreprocess(double preprocess) =>
            (preprocess < 0.0 || double.IsNaN(preprocess)) ? 0.0 : preprocess;
    }
}
