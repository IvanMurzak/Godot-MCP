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
    /// The boolean Constructive-Solid-Geometry operation a CSG node applies to its siblings/children: a
    /// <c>Union</c> (merge), an <c>Intersection</c> (keep the overlap), or a <c>Subtraction</c> (carve away).
    /// Pure-managed (no Godot native types) so the parse/label logic is CI-unit-testable.
    ///
    /// <para>
    /// The integer values are pinned to MATCH Godot's <c>CsgShape3D.OperationEnum</c> ordering
    /// (<c>Union</c>=0, <c>Intersection</c>=1, <c>Subtraction</c>=2), so the editor tools can cast between
    /// this pure-managed enum and the Godot enum without a lookup table (the cast is exercised by the E2E leg).
    /// </para>
    /// </summary>
    public enum CsgOperation
    {
        /// <summary>Merge the shape into its siblings (Godot <c>OperationEnum.Union</c>).</summary>
        Union = 0,

        /// <summary>Keep only the overlapping volume (Godot <c>OperationEnum.Intersection</c>).</summary>
        Intersection = 1,

        /// <summary>Carve the shape's volume away from its siblings (Godot <c>OperationEnum.Subtraction</c>).</summary>
        Subtraction = 2
    }

    /// <summary>Pure-managed helpers for <see cref="CsgOperation"/> parsing and display.</summary>
    public static class CsgOperations
    {
        /// <summary>The short display label for an operation (<c>"Union"</c> / <c>"Intersection"</c> / <c>"Subtraction"</c>).</summary>
        public static string ToLabel(this CsgOperation operation) => operation switch
        {
            CsgOperation.Union => "Union",
            CsgOperation.Intersection => "Intersection",
            CsgOperation.Subtraction => "Subtraction",
            _ => "Union"
        };

        /// <summary>
        /// Parse a user/LLM-supplied operation string into a <see cref="CsgOperation"/>. Accepts (case- and
        /// whitespace-insensitive): <c>"union"</c>/<c>"add"</c>/<c>"merge"</c> → <see cref="CsgOperation.Union"/>;
        /// <c>"intersection"</c>/<c>"intersect"</c> → <see cref="CsgOperation.Intersection"/>;
        /// <c>"subtraction"</c>/<c>"subtract"</c>/<c>"sub"</c> → <see cref="CsgOperation.Subtraction"/>.
        /// Returns false for anything else (and for null/empty).
        /// </summary>
        public static bool TryParse(string? value, out CsgOperation operation)
        {
            operation = CsgOperation.Union;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value!.Trim().ToLowerInvariant())
            {
                case "union":
                case "add":
                case "merge":
                    operation = CsgOperation.Union;
                    return true;
                case "intersection":
                case "intersect":
                    operation = CsgOperation.Intersection;
                    return true;
                case "subtraction":
                case "subtract":
                case "sub":
                    operation = CsgOperation.Subtraction;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Parse an operation string, throwing <see cref="ArgumentException"/> on an unrecognized value. Used
        /// by tools that require an explicit, valid operation.
        /// </summary>
        public static CsgOperation Parse(string? value)
        {
            if (TryParse(value, out var operation))
                return operation;
            throw new ArgumentException(
                $"Unrecognized CSG operation '{value}'. Use 'Union', 'Intersection', or 'Subtraction'.", nameof(value));
        }
    }
}
