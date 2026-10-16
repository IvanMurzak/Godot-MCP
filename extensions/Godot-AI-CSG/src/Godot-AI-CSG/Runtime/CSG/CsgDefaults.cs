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
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for two things shared by the
    /// editor-driving CSG tools and the pure-managed <c>csg-defaults</c> tool:
    /// <list type="number">
    ///   <item>a recommended starter configuration per <see cref="CsgPrimitiveKind"/>, and</item>
    ///   <item>the value-clamping rules the editor tools apply before writing to a live node (so an LLM can
    ///         never push a CSG node into an invalid state — a non-positive size/radius/height, or a sphere
    ///         with too few radial segments to form a surface).</item>
    /// </list>
    /// Keeping this logic pure-managed means it is verified by fast xUnit tests with no Godot binary, and the
    /// editor tools simply reuse it.
    /// </summary>
    public static class CsgDefaults
    {
        /// <summary>The recommended starter uniform box size (Godot <c>Size</c> on each axis).</summary>
        public const double DefaultBoxSize = 2.0;

        /// <summary>The recommended starter radius for a sphere/cylinder.</summary>
        public const double DefaultRadius = 0.5;

        /// <summary>The recommended starter cylinder height.</summary>
        public const double DefaultHeight = 2.0;

        /// <summary>The recommended starter sphere radial-segment count.</summary>
        public const int DefaultRadialSegments = 12;

        /// <summary>The smallest positive dimension a clamp will yield (never 0 — Godot needs a positive extent).</summary>
        public const double MinDimension = 0.001;

        /// <summary>The fewest radial segments a CSG sphere can hold. Godot's <c>CSGSphere3D</c> engine setter
        /// itself floors <c>radial_segments</c> to 4 (a lower request is silently raised to 4 on the live node),
        /// so the managed clamp mirrors that floor — otherwise the reported value would not match what the node
        /// actually stores.</summary>
        public const int MinRadialSegments = 4;

        /// <summary>
        /// A recommended starter configuration for the given kind, as a fully-populated
        /// <see cref="CsgShapeInfo"/> (no bound node, so <see cref="CsgShapeInfo.NodePath"/> /
        /// <see cref="CsgShapeInfo.TypeName"/> are empty). The <c>csg-defaults</c> tool returns this.
        /// </summary>
        public static CsgShapeInfo For(CsgPrimitiveKind kind)
        {
            var info = new CsgShapeInfo
            {
                NodePath = string.Empty,
                Kind = kind.ToLabel(),
                TypeName = string.Empty,
                Operation = CsgOperation.Union.ToLabel()
            };

            switch (kind)
            {
                case CsgPrimitiveKind.Box:
                    info.SizeX = DefaultBoxSize;
                    info.SizeY = DefaultBoxSize;
                    info.SizeZ = DefaultBoxSize;
                    break;
                case CsgPrimitiveKind.Sphere:
                    info.Radius = DefaultRadius;
                    info.RadialSegments = DefaultRadialSegments;
                    break;
                case CsgPrimitiveKind.Cylinder:
                    info.Radius = DefaultRadius;
                    info.Height = DefaultHeight;
                    break;
                case CsgPrimitiveKind.Combiner:
                    // Container only — no primitive scalars.
                    break;
            }

            return info;
        }

        /// <summary>Clamp a box extent to a strictly-positive size (Godot needs a positive extent).</summary>
        public static double ClampSize(double size) =>
            (size <= 0.0 || double.IsNaN(size)) ? MinDimension : size;

        /// <summary>Clamp a radius to a strictly-positive value.</summary>
        public static double ClampRadius(double radius) =>
            (radius <= 0.0 || double.IsNaN(radius)) ? MinDimension : radius;

        /// <summary>Clamp a cylinder height to a strictly-positive value.</summary>
        public static double ClampHeight(double height) =>
            (height <= 0.0 || double.IsNaN(height)) ? MinDimension : height;

        /// <summary>Clamp a sphere's radial-segment count to Godot's engine floor (&gt;= 4).</summary>
        public static int ClampRadialSegments(int segments) =>
            segments < MinRadialSegments ? MinRadialSegments : segments;
    }
}
