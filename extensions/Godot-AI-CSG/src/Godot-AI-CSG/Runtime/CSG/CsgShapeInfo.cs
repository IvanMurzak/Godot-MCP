/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>
    /// Pure-managed, serializable snapshot of a Godot CSG node's scalar configuration — the structured result
    /// every <c>csg-*</c> tool returns. Holds ONLY primitives + strings (no Godot native types), so it is safe
    /// to build inside a <c>MainThread.Instance.Run(...)</c> delegate and return across the tool boundary, it
    /// serializes cleanly through ReflectorNet, and the pure-managed defaults helper can produce one with no
    /// Godot binary (CI-unit-testable).
    ///
    /// <para>
    /// One shape covers all four CSG node types. Type-specific scalars (<see cref="SizeX"/>… for a box,
    /// <see cref="Radius"/>/<see cref="RadialSegments"/> for a sphere, <see cref="Radius"/>/<see cref="Height"/>
    /// for a cylinder) are 0 when not applicable; <see cref="Kind"/> says which apply.
    /// </para>
    /// </summary>
    public sealed class CsgShapeInfo
    {
        /// <summary>Scene path of the node (empty for a defaults snapshot that is not bound to a node).</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>
        /// The extension-produced, stable kind label: <c>"Box"</c> / <c>"Sphere"</c> / <c>"Cylinder"</c> /
        /// <c>"Combiner"</c>. Prefer asserting THIS over <see cref="TypeName"/> — it never depends on Godot's
        /// internal <c>Node.GetClass()</c> casing.
        /// </summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The node's Godot engine class name (e.g. <c>"CSGBox3D"</c>); empty for a defaults snapshot.</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>The boolean operation the node applies: <c>"Union"</c> / <c>"Intersection"</c> / <c>"Subtraction"</c>.</summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>Box width along X (Godot <c>CsgBox3D.Size.X</c>); 0 for non-box nodes.</summary>
        public double SizeX { get; set; }

        /// <summary>Box height along Y (Godot <c>CsgBox3D.Size.Y</c>); 0 for non-box nodes.</summary>
        public double SizeY { get; set; }

        /// <summary>Box depth along Z (Godot <c>CsgBox3D.Size.Z</c>); 0 for non-box nodes.</summary>
        public double SizeZ { get; set; }

        /// <summary>Sphere/cylinder radius (Godot <c>Radius</c>); 0 for box/combiner nodes.</summary>
        public double Radius { get; set; }

        /// <summary>Cylinder height (Godot <c>CsgCylinder3D.Height</c>); 0 for non-cylinder nodes.</summary>
        public double Height { get; set; }

        /// <summary>Sphere radial segment count (Godot <c>CsgSphere3D.RadialSegments</c>); 0 for non-sphere nodes.</summary>
        public int RadialSegments { get; set; }
    }
}
