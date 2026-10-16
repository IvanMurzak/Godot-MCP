/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Particles
{
    /// <summary>
    /// Pure-managed, serializable snapshot of a Godot particle node's scalar configuration — the structured
    /// result every <c>particles-*</c> tool returns. Holds ONLY primitives + strings (no Godot native types),
    /// so it is safe to build inside a <c>MainThread.Instance.Run(...)</c> delegate and return across the tool
    /// boundary, it serializes cleanly through ReflectorNet, and the pure-managed defaults helper can produce
    /// one with no Godot binary (CI-unit-testable).
    ///
    /// <para>
    /// The scalar fields mirror the cross-version-stable properties shared by <c>GpuParticles2D</c> and
    /// <c>GpuParticles3D</c> (identical names on Godot 4.3 … 4.5), so one shape covers both node types.
    /// </para>
    /// </summary>
    public sealed class ParticlesNodeInfo
    {
        /// <summary>Scene path of the node (empty for a defaults snapshot that is not bound to a node).</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>Display dimension: <c>"2D"</c> or <c>"3D"</c>.</summary>
        public string Dimension { get; set; } = string.Empty;

        /// <summary>The node's Godot class name (e.g. <c>"GpuParticles3D"</c>); empty for a defaults snapshot.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Number of particles emitted (Godot <c>amount</c>; always &gt;= 1).</summary>
        public int Amount { get; set; }

        /// <summary>Particle lifetime in seconds (Godot <c>lifetime</c>; always &gt; 0).</summary>
        public double Lifetime { get; set; }

        /// <summary>Whether the emitter emits a single burst then stops (Godot <c>one_shot</c>).</summary>
        public bool OneShot { get; set; }

        /// <summary>Whether the emitter is currently emitting (Godot <c>emitting</c>).</summary>
        public bool Emitting { get; set; }

        /// <summary>Emission burst ratio 0..1 (Godot <c>explosiveness</c>).</summary>
        public float Explosiveness { get; set; }

        /// <summary>Emission timing randomness 0..1 (Godot <c>randomness</c>).</summary>
        public float Randomness { get; set; }

        /// <summary>Simulation speed multiplier (Godot <c>speed_scale</c>; always &gt;= 0).</summary>
        public double SpeedScale { get; set; }

        /// <summary>Seconds of pre-simulation applied on start (Godot <c>preprocess</c>; always &gt;= 0).</summary>
        public double Preprocess { get; set; }

        /// <summary>Whether particles are simulated in the node's local space (Godot <c>local_coords</c>).</summary>
        public bool LocalCoords { get; set; }
    }
}
