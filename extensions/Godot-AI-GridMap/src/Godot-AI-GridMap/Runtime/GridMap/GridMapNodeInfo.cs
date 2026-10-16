/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    /// <summary>
    /// Pure-managed, serializable snapshot of a Godot <c>GridMap</c> node's scalar configuration — the
    /// structured result the node-level <c>gridmap-*</c> tools (<c>create</c>, <c>set-mesh-library</c>,
    /// <c>clear</c>, <c>get</c>, <c>defaults</c>) return. Holds ONLY primitives + strings (no Godot native
    /// types), so it is safe to build inside a <c>MainThread.Instance.Run(...)</c> delegate and return across
    /// the tool boundary, it serializes cleanly through ReflectorNet, and the pure-managed defaults helper can
    /// produce one with no Godot binary (CI-unit-testable).
    ///
    /// <para>
    /// The scalar fields mirror the cross-version-stable <c>GridMap</c> properties (identical names on Godot
    /// 4.3 … 4.5). <see cref="Kind"/> is an extension-PRODUCED constant ("GridMap") — prefer asserting it (and
    /// the produced scalar values) over <see cref="TypeName"/> in E2E, since <c>Node.GetClass()</c> returns the
    /// engine's internal class name whose casing can differ from the C# binding.
    /// </para>
    /// </summary>
    public sealed class GridMapNodeInfo
    {
        /// <summary>Scene path of the node (empty for a defaults snapshot that is not bound to a node).</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>Extension-produced kind label — always <c>"GridMap"</c> (stable; prefer over <see cref="TypeName"/>).</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The node's Godot class name (e.g. <c>"GridMap"</c>); empty for a defaults snapshot.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>Cell size X (Godot <c>cell_size.x</c>; always &gt; 0).</summary>
        public double CellSizeX { get; set; }

        /// <summary>Cell size Y (Godot <c>cell_size.y</c>; always &gt; 0).</summary>
        public double CellSizeY { get; set; }

        /// <summary>Cell size Z (Godot <c>cell_size.z</c>; always &gt; 0).</summary>
        public double CellSizeZ { get; set; }

        /// <summary>Mesh scale applied to each cell (Godot <c>cell_scale</c>).</summary>
        public double CellScale { get; set; }

        /// <summary>Octant size used for batching (Godot <c>cell_octant_size</c>).</summary>
        public int CellOctantSize { get; set; }

        /// <summary>Whether cells are centered on the X axis (Godot <c>cell_center_x</c>).</summary>
        public bool CellCenterX { get; set; }

        /// <summary>Whether cells are centered on the Y axis (Godot <c>cell_center_y</c>).</summary>
        public bool CellCenterY { get; set; }

        /// <summary>Whether cells are centered on the Z axis (Godot <c>cell_center_z</c>).</summary>
        public bool CellCenterZ { get; set; }

        /// <summary>res:// path of the assigned <c>MeshLibrary</c> resource (empty when none is assigned).</summary>
        public string MeshLibraryPath { get; set; } = string.Empty;

        /// <summary>Number of non-empty cells in the grid (Godot <c>get_used_cells().Count</c>).</summary>
        public int CellCount { get; set; }
    }
}
