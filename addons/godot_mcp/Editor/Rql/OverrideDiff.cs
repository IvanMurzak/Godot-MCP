/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The pure part of a scene instance's <c>overrides</c>: whether a live property value differs from the template's, and a
    /// transform difference told as position / rotation_degrees / scale (what an agent sets) rather than one 3x4 matrix. Godot's
    /// math structs are plain C#, so this is unit-tested.
    /// </summary>
    public static class OverrideDiff
    {
        private const float Epsilon = 1e-4f;

        /// <summary>The (property, live, template) differences of two transforms, by position, rotation_degrees and scale.</summary>
        public static IEnumerable<(string property, object live, object template)> Transform(Transform3D live, Transform3D template)
        {
            if (!Near(live.Origin, template.Origin)) yield return ("position", live.Origin, template.Origin);
            var liveRot = Degrees(live.Basis.GetEuler());
            var templateRot = Degrees(template.Basis.GetEuler());
            if (!Near(liveRot, templateRot)) yield return ("rotation_degrees", liveRot, templateRot);
            if (!Near(live.Basis.Scale, template.Basis.Scale)) yield return ("scale", live.Basis.Scale, template.Basis.Scale);
        }

        /// <inheritdoc cref="Transform(Transform3D, Transform3D)"/>
        public static IEnumerable<(string property, object live, object template)> Transform(Transform2D live, Transform2D template)
        {
            if (!Near(live.Origin, template.Origin)) yield return ("position", live.Origin, template.Origin);
            var liveRot = Mathf.RadToDeg(live.Rotation);
            var templateRot = Mathf.RadToDeg(template.Rotation);
            if (Math.Abs(liveRot - templateRot) > 1e-3f) yield return ("rotation_degrees", liveRot, templateRot);
            if (!Near(live.Scale, template.Scale)) yield return ("scale", live.Scale, template.Scale);
        }

        /// <summary>
        /// Float-noise tolerant equality for numbers, vectors and colors (what an edited value is compared by), or null for any
        /// other kind (the caller compares those exactly).
        /// </summary>
        public static bool? ApproxSame(object? live, object? template) => (live, template) switch
        {
            (float a, float b) => Math.Abs(a - b) <= Epsilon,
            (double a, double b) => Math.Abs(a - b) <= Epsilon,
            (Vector2 a, Vector2 b) => Near(a, b),
            (Vector3 a, Vector3 b) => Near(a, b),
            (Color a, Color b) => a.IsEqualApprox(b),
            _ => null,
        };

        private static Vector3 Degrees(Vector3 radians) => new Vector3(Mathf.RadToDeg(radians.X), Mathf.RadToDeg(radians.Y), Mathf.RadToDeg(radians.Z));
        private static bool Near(Vector3 a, Vector3 b) => (a - b).Length() <= Epsilon;
        private static bool Near(Vector2 a, Vector2 b) => (a - b).Length() <= Epsilon;
    }
}
#endif
