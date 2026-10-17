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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>
    /// The value rules the module's adapters enforce before a navigation node is written (an RQL <c>validate</c> runs before
    /// any write, so a rejected value fails the whole mutation and changes nothing), and the preconditions of the bake calls.
    /// Each returns the rejection reason, or null when the value is valid. Pure-managed (no Godot API), so it is unit-tested.
    /// </summary>
    public static class NavigationRules
    {
        /// <summary>
        /// An agent's avoidance radius: above 0 (0.x clamped it to a small positive value; Godot itself logs an error for a
        /// negative radius and keeps the old one).
        /// </summary>
        public static string? Radius(float value) =>
            value > 0f && Finite(value) ? null : $"Radius must be above 0 (got {Text(value)})";

        /// <summary>
        /// Distances, speeds, times and costs that cannot be negative: PathDesiredDistance, TargetDesiredDistance (0.x clamped
        /// them), MaxSpeed, Height, TimeHorizonAgents / TimeHorizonObstacles, EnterCost / TravelCost (Godot logs an error for a
        /// negative value and keeps the old one).
        /// </summary>
        public static string? NonNegative(string member, float value) =>
            value >= 0f && Finite(value) ? null : $"{member} must be 0 or more (got {Text(value)})";

        /// <summary>AvoidancePriority: Godot refuses a value outside 0 to 1 (logs an error, keeps the old one).</summary>
        public static string? Priority(float value) =>
            value >= 0f && value <= 1f ? null : $"AvoidancePriority must be between 0 and 1 (got {Text(value)})";

        /// <summary>
        /// Whether a region can bake: it needs its navigation resource (Godot logs an error without one) and must not be
        /// baking already. <paramref name="resource"/> names it ("NavigationMesh" / "NavigationPolygon").
        /// </summary>
        public static string? CanBake(string region, string resource, bool hasResource, bool baking) =>
            !hasResource ? $"{region} has no {resource} to bake into: set {resource} = {{\"$type\": \"{resource}\"}} first"
            : baking ? $"{region} is already baking"
            : null;

        /// <summary>A NavigationPolygon outline is a closed shape: at least 3 points (fewer make the bake fail).</summary>
        public static string? Outline(int points) =>
            points >= 3 ? null : $"an outline needs at least 3 points (got {points})";

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
