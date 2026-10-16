/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// Pure-managed, serializable snapshot of a Terrain3D node's scalar configuration — the structured result
    /// every <c>terrain3d-*</c> EDITOR tool returns (CLASS-B Option A). It embeds the presence-gate fields
    /// (<see cref="Installed"/> / <see cref="Addon"/> / <see cref="MissingClass"/> / <see cref="Hint"/>) so a
    /// single result shape covers BOTH the "addon not installed" path (gate fields set, node fields default)
    /// and the happy path (gate <see cref="Installed"/> = true, node fields populated). Holds ONLY primitives
    /// + strings (no Godot native types), so it is safe to build inside a <c>MainThread.Instance.Run(...)</c>
    /// delegate, serializes cleanly through ReflectorNet (PascalCase property names), and the gate factory is
    /// CI-unit-testable with no Godot binary.
    /// </summary>
    public sealed class Terrain3DNodeInfo
    {
        /// <summary>Whether the Terrain3D addon is installed (the presence gate passed).</summary>
        public bool Installed { get; set; }

        /// <summary>The wrapped addon's display name (always <c>"Terrain3D"</c>).</summary>
        public string Addon { get; set; } = AddonGate.AddonName;

        /// <summary>The missing class when <see cref="Installed"/> is false; otherwise null.</summary>
        public string? MissingClass { get; set; }

        /// <summary>Install hint when <see cref="Installed"/> is false; otherwise empty.</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>Scene path of the Terrain3D node (empty when the gate failed).</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>The node's Godot class name (e.g. <c>"Terrain3D"</c>); empty when the gate failed.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>The terrain's <c>data_directory</c> (res:// path where region data persists).</summary>
        public string DataDirectory { get; set; } = string.Empty;

        /// <summary>The terrain's <c>region_size</c> (region edge length in cells).</summary>
        public int RegionSize { get; set; }

        /// <summary>The Terrain3D addon <c>version</c> string reported by the live node.</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>Whether the terrain has a <c>Terrain3DMaterial</c> assigned.</summary>
        public bool HasMaterial { get; set; }

        /// <summary>The terrain's <c>mesh_lods</c> (number of clipmap mesh LOD levels).</summary>
        public int MeshLods { get; set; }

        /// <summary>The terrain's <c>mesh_size</c> (clipmap mesh size).</summary>
        public int MeshSize { get; set; }

        /// <summary>The terrain's <c>vertex_spacing</c> (world-space distance between vertices).</summary>
        public double VertexSpacing { get; set; }

        /// <summary>Number of active regions in the terrain's <c>Terrain3DData</c> (0 when none/empty).</summary>
        public int RegionCount { get; set; }

        /// <summary>Build a gate-failure result (addon absent) from an <see cref="AddonGateResult"/>.</summary>
        public static Terrain3DNodeInfo FromGate(AddonGateResult gate) => new()
        {
            Installed = gate.Installed,
            Addon = gate.Addon,
            MissingClass = gate.MissingClass,
            Hint = gate.Hint
        };
    }
}
