/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>
    /// Pure-managed, serializable snapshot of a Godot navigation node's scalar configuration — the structured
    /// result every <c>navigation-*</c> tool returns. Holds ONLY primitives + strings (no Godot native types,
    /// not even <c>Vector2</c>/<c>Vector3</c> — link positions are flattened to <c>X/Y/Z</c> doubles), so it is
    /// safe to build inside a <c>MainThread.Instance.Run(...)</c> delegate and return across the tool boundary,
    /// it serializes cleanly through ReflectorNet, and the pure-managed defaults helper can produce one with no
    /// Godot binary (CI-unit-testable).
    ///
    /// <para>
    /// One shape covers all three navigation families (region / agent / link). <see cref="Kind"/> says which
    /// family the node is; fields that do not apply to that family stay at their defaults. The scalar property
    /// names mirror the cross-version-stable Godot 4.x navigation API (identical on the 2D and 3D node of each
    /// family), so one shape covers both dimensions.
    /// </para>
    /// </summary>
    public sealed class NavigationNodeInfo
    {
        /// <summary>Scene path of the node (empty for a defaults snapshot that is not bound to a node).</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>Display dimension: <c>"2D"</c> or <c>"3D"</c>.</summary>
        public string Dimension { get; set; } = string.Empty;

        /// <summary>Navigation family: <c>"Region"</c>, <c>"Agent"</c> or <c>"Link"</c>.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The node's Godot class name (e.g. <c>"NavigationRegion3D"</c>); empty for a defaults snapshot.</summary>
        public string TypeName { get; set; } = string.Empty;

        // ── Region / Link ─────────────────────────────────────────────────────────────────────────────────
        /// <summary>Whether the region/link is enabled (Godot <c>enabled</c>). Region &amp; Link only.</summary>
        public bool Enabled { get; set; }

        // ── Region ────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// <c>res://</c> path of the region's assigned navigation resource (NavigationPolygon 2D /
        /// NavigationMesh 3D), or empty when none is assigned or the resource is in-memory. Region only.
        /// </summary>
        public string ResourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Godot class name of the region's assigned navigation resource (<c>"NavigationPolygon"</c> /
        /// <c>"NavigationMesh"</c>), or empty when none is assigned. Region only.
        /// </summary>
        public string ResourceType { get; set; } = string.Empty;

        // ── Agent ─────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Avoidance radius (Godot <c>radius</c>; always &gt; 0). Agent only.</summary>
        public float Radius { get; set; }

        /// <summary>Distance to the next path point at which it is considered reached (Godot <c>path_desired_distance</c>; &gt;= 0). Agent only.</summary>
        public float PathDesiredDistance { get; set; }

        /// <summary>Distance to the final target at which it is considered reached (Godot <c>target_desired_distance</c>; &gt;= 0). Agent only.</summary>
        public float TargetDesiredDistance { get; set; }

        /// <summary>Maximum movement speed used for avoidance (Godot <c>max_speed</c>; &gt;= 0). Agent only.</summary>
        public float MaxSpeed { get; set; }

        /// <summary>Whether RVO avoidance is enabled (Godot <c>avoidance_enabled</c>). Agent only.</summary>
        public bool AvoidanceEnabled { get; set; }

        // ── Link ──────────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Link start position X (Godot <c>start_position</c>). Link only.</summary>
        public double StartX { get; set; }

        /// <summary>Link start position Y (Godot <c>start_position</c>). Link only.</summary>
        public double StartY { get; set; }

        /// <summary>Link start position Z (Godot <c>start_position</c>, 3D only). Link only.</summary>
        public double StartZ { get; set; }

        /// <summary>Link end position X (Godot <c>end_position</c>). Link only.</summary>
        public double EndX { get; set; }

        /// <summary>Link end position Y (Godot <c>end_position</c>). Link only.</summary>
        public double EndY { get; set; }

        /// <summary>Link end position Z (Godot <c>end_position</c>, 3D only). Link only.</summary>
        public double EndZ { get; set; }

        /// <summary>Whether the link can be traversed in both directions (Godot <c>bidirectional</c>). Link only.</summary>
        public bool Bidirectional { get; set; }
    }
}
