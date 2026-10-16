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

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>
    /// Which built-in Godot CSG node a tool targets: a <c>CsgBox3D</c>, <c>CsgSphere3D</c>,
    /// <c>CsgCylinder3D</c> primitive, or a <c>CsgCombiner3D</c> container (the root that groups child CSG
    /// shapes for a boolean op). Pure-managed (no Godot native types) so the parse logic is CI-unit-testable.
    /// </summary>
    public enum CsgPrimitiveKind
    {
        /// <summary><c>CsgBox3D</c> — an axis-aligned box primitive.</summary>
        Box,

        /// <summary><c>CsgSphere3D</c> — a sphere primitive.</summary>
        Sphere,

        /// <summary><c>CsgCylinder3D</c> — a cylinder primitive.</summary>
        Cylinder,

        /// <summary><c>CsgCombiner3D</c> — a container that groups child CSG shapes for a boolean operation.</summary>
        Combiner
    }

    /// <summary>Pure-managed helpers for <see cref="CsgPrimitiveKind"/> parsing and display.</summary>
    public static class CsgPrimitiveKinds
    {
        /// <summary>The short display label for a kind (<c>"Box"</c> / <c>"Sphere"</c> / <c>"Cylinder"</c> / <c>"Combiner"</c>).</summary>
        public static string ToLabel(this CsgPrimitiveKind kind) => kind switch
        {
            CsgPrimitiveKind.Box => "Box",
            CsgPrimitiveKind.Sphere => "Sphere",
            CsgPrimitiveKind.Cylinder => "Cylinder",
            CsgPrimitiveKind.Combiner => "Combiner",
            _ => "Box"
        };

        /// <summary>
        /// The Godot ENGINE class name for a kind (<c>"CSGBox3D"</c> / <c>"CSGSphere3D"</c> /
        /// <c>"CSGCylinder3D"</c> / <c>"CSGCombiner3D"</c>) — note the engine spells the acronym <c>CSG</c>
        /// in upper-case, unlike the C# binding type (<c>CsgBox3D</c>). This is exactly why tools surface a
        /// stable extension-produced <see cref="CsgShapeInfo.Kind"/> instead of <c>Node.GetClass()</c> casing.
        /// </summary>
        public static string ToGodotClassName(this CsgPrimitiveKind kind) => kind switch
        {
            CsgPrimitiveKind.Box => "CSGBox3D",
            CsgPrimitiveKind.Sphere => "CSGSphere3D",
            CsgPrimitiveKind.Cylinder => "CSGCylinder3D",
            CsgPrimitiveKind.Combiner => "CSGCombiner3D",
            _ => "CSGBox3D"
        };

        /// <summary>
        /// Parse a user/LLM-supplied kind string into a <see cref="CsgPrimitiveKind"/>. Accepts (case- and
        /// whitespace-insensitive): <c>"box"</c>, <c>"sphere"</c>, <c>"cylinder"</c>, <c>"combiner"</c>.
        /// Returns false for anything else (and for null/empty).
        /// </summary>
        public static bool TryParse(string? value, out CsgPrimitiveKind kind)
        {
            kind = CsgPrimitiveKind.Box;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value!.Trim().ToLowerInvariant())
            {
                case "box":
                    kind = CsgPrimitiveKind.Box;
                    return true;
                case "sphere":
                    kind = CsgPrimitiveKind.Sphere;
                    return true;
                case "cylinder":
                    kind = CsgPrimitiveKind.Cylinder;
                    return true;
                case "combiner":
                    kind = CsgPrimitiveKind.Combiner;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Parse a kind string, throwing <see cref="ArgumentException"/> on an unrecognized value. Used by
        /// tools that require an explicit, valid kind.
        /// </summary>
        public static CsgPrimitiveKind Parse(string? value)
        {
            if (TryParse(value, out var kind))
                return kind;
            throw new ArgumentException(
                $"Unrecognized CSG kind '{value}'. Use 'box', 'sphere', 'cylinder', or 'combiner'.", nameof(value));
        }
    }
}
