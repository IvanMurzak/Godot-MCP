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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>
    /// Which dimension a navigation node targets: a 2D node (<c>NavigationRegion2D</c> /
    /// <c>NavigationAgent2D</c> / <c>NavigationLink2D</c>) or a 3D node (<c>NavigationRegion3D</c> /
    /// <c>NavigationAgent3D</c> / <c>NavigationLink3D</c>). Pure-managed (no Godot native types) so the parse
    /// logic is CI-unit-testable with no Godot binary.
    /// </summary>
    public enum NavigationDimension
    {
        /// <summary>The 2D navigation family (<c>Navigation*2D</c>).</summary>
        Two,

        /// <summary>The 3D navigation family (<c>Navigation*3D</c>).</summary>
        Three
    }

    /// <summary>
    /// Which built-in Godot navigation node family a tool targets. Pure-managed so the (kind, dimension) →
    /// Godot class-name mapping (<see cref="NavigationClassNames"/>) is unit-testable.
    /// </summary>
    public enum NavigationKind
    {
        /// <summary>A navigation region (<c>NavigationRegion2D</c> / <c>NavigationRegion3D</c>).</summary>
        Region,

        /// <summary>A navigation agent (<c>NavigationAgent2D</c> / <c>NavigationAgent3D</c>).</summary>
        Agent,

        /// <summary>A navigation link (<c>NavigationLink2D</c> / <c>NavigationLink3D</c>).</summary>
        Link
    }

    /// <summary>Pure-managed helpers for <see cref="NavigationDimension"/> parsing and display.</summary>
    public static class NavigationDimensions
    {
        /// <summary>The short display label for a dimension (<c>"2D"</c> / <c>"3D"</c>).</summary>
        public static string ToLabel(this NavigationDimension dimension) =>
            dimension == NavigationDimension.Two ? "2D" : "3D";

        /// <summary>
        /// Parse a user/LLM-supplied dimension string into a <see cref="NavigationDimension"/>. Accepts (case-
        /// and whitespace-insensitive): <c>"2D"</c>, <c>"2"</c>, <c>"two"</c> → <see cref="NavigationDimension.Two"/>;
        /// <c>"3D"</c>, <c>"3"</c>, <c>"three"</c> → <see cref="NavigationDimension.Three"/>. Returns false for
        /// anything else (and for null/empty).
        /// </summary>
        public static bool TryParse(string? value, out NavigationDimension dimension)
        {
            dimension = NavigationDimension.Three;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value!.Trim().ToLowerInvariant())
            {
                case "2d":
                case "2":
                case "two":
                    dimension = NavigationDimension.Two;
                    return true;
                case "3d":
                case "3":
                case "three":
                    dimension = NavigationDimension.Three;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Parse a dimension string, throwing <see cref="ArgumentException"/> on an unrecognized value. Used
        /// by tools that require an explicit, valid dimension.
        /// </summary>
        public static NavigationDimension Parse(string? value)
        {
            if (TryParse(value, out var dimension))
                return dimension;
            throw new ArgumentException(
                $"Unrecognized navigation dimension '{value}'. Use '2D' or '3D'.", nameof(value));
        }
    }

    /// <summary>
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for the Godot class name a
    /// (kind, dimension) pair maps to — e.g. (<see cref="NavigationKind.Region"/>, <see cref="NavigationDimension.Three"/>)
    /// → <c>"NavigationRegion3D"</c>. The editor tools instantiate the matching node type; the unit tests pin
    /// these strings so the class names the tools rely on can never drift silently.
    /// </summary>
    public static class NavigationClassNames
    {
        /// <summary>The Godot class name for a navigation (kind, dimension) pair.</summary>
        public static string For(NavigationKind kind, NavigationDimension dimension)
        {
            var suffix = dimension == NavigationDimension.Two ? "2D" : "3D";
            return kind switch
            {
                NavigationKind.Region => $"NavigationRegion{suffix}",
                NavigationKind.Agent => $"NavigationAgent{suffix}",
                NavigationKind.Link => $"NavigationLink{suffix}",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown navigation kind.")
            };
        }

        /// <summary>The short display label for a navigation kind (<c>"Region"</c> / <c>"Agent"</c> / <c>"Link"</c>).</summary>
        public static string ToLabel(this NavigationKind kind) => kind switch
        {
            NavigationKind.Region => "Region",
            NavigationKind.Agent => "Agent",
            NavigationKind.Link => "Link",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown navigation kind.")
        };
    }
}
