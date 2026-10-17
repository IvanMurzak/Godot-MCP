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

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>
    /// The value rules the module's adapters enforce before a CSG shape is written (an RQL <c>validate</c> runs before any
    /// write, so a rejected value fails the whole mutation and changes nothing). Each returns the rejection reason, or null
    /// when the value is valid. Pure-managed (no Godot API), so it is unit-tested.
    /// </summary>
    public static class CsgRules
    {
        /// <summary>Godot's CSGSphere3D raises fewer radial segments to 4 without telling (0.x clamped the same way).</summary>
        public const int MinRadialSegments = 4;

        /// <summary>Godot's CSGSphere3D raises fewer rings to 1.</summary>
        public const int MinRings = 1;

        /// <summary>CSGCylinder3D / CSGTorus3D refuse fewer than 3 sides (<c>set_sides</c> / <c>set_ring_sides</c> log an error).</summary>
        public const int MinSides = 3;

        /// <summary>
        /// A dimension that must be above 0: CSGSphere3D.Radius (Godot's <c>set_radius</c> logs an error and keeps the old
        /// value), CSGCylinder3D.Radius / Height (0.x clamped them to a positive value).
        /// </summary>
        public static string? Positive(string member, float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)
                ? null
                : $"{member} must be above 0 (got {Text(value)})";

        /// <summary>CSGBox3D.Size: every axis above 0 (0.x clamped each one to a positive value).</summary>
        public static string? Size(float x, float y, float z) =>
            Positive("x", x) == null && Positive("y", y) == null && Positive("z", z) == null
                ? null
                : $"Size must be above 0 on every axis (got ({Text(x)}, {Text(y)}, {Text(z)}))";

        /// <summary>A whole-number count with a floor (radial segments, rings, sides).</summary>
        public static string? AtLeast(string member, int value, int min) =>
            value >= min ? null : $"{member} must be at least {min} (got {value})";

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
