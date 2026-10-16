/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for two things shared by the
    /// editor-driving navigation tools and the pure-managed <c>navigation-defaults</c> tool:
    /// <list type="number">
    ///   <item>a recommended starter <see cref="NavigationAgent2D"/>/<c>3D</c> configuration per
    ///         <see cref="NavigationDimension"/> (2D works in pixels, 3D in metres, so the sane defaults differ), and</item>
    ///   <item>the value-clamping rules the <c>navigation-agent-configure</c> tool applies before writing to a
    ///         live node (so an LLM can never push an agent into an invalid state — non-positive radius,
    ///         negative distances, negative speed).</item>
    /// </list>
    /// Keeping this logic pure-managed means it is verified by fast xUnit tests with no Godot binary, and the
    /// editor tools simply reuse it.
    /// </summary>
    public static class NavigationDefaults
    {
        // ── Recommended agent starter values (mirror Godot's own per-dimension defaults) ────────────────────

        /// <summary>Recommended 2D agent <c>radius</c> (pixels).</summary>
        public const float Default2DRadius = 10f;

        /// <summary>Recommended 2D agent <c>path_desired_distance</c> (pixels).</summary>
        public const float Default2DPathDesiredDistance = 20f;

        /// <summary>Recommended 2D agent <c>target_desired_distance</c> (pixels).</summary>
        public const float Default2DTargetDesiredDistance = 10f;

        /// <summary>Recommended 2D agent <c>max_speed</c> (pixels/second).</summary>
        public const float Default2DMaxSpeed = 100f;

        /// <summary>Recommended 3D agent <c>radius</c> (metres).</summary>
        public const float Default3DRadius = 0.5f;

        /// <summary>Recommended 3D agent <c>path_desired_distance</c> (metres).</summary>
        public const float Default3DPathDesiredDistance = 1.0f;

        /// <summary>Recommended 3D agent <c>target_desired_distance</c> (metres).</summary>
        public const float Default3DTargetDesiredDistance = 1.0f;

        /// <summary>Recommended 3D agent <c>max_speed</c> (metres/second).</summary>
        public const float Default3DMaxSpeed = 10f;

        /// <summary>
        /// A recommended starter agent configuration for the given dimension, as a fully-populated
        /// <see cref="NavigationNodeInfo"/> with <see cref="NavigationNodeInfo.Kind"/> = <c>"Agent"</c> and no
        /// bound node. The <c>navigation-defaults</c> tool returns this.
        /// </summary>
        public static NavigationNodeInfo ForAgent(NavigationDimension dimension)
        {
            var is2D = dimension == NavigationDimension.Two;
            return new NavigationNodeInfo
            {
                NodePath = string.Empty,
                Dimension = dimension.ToLabel(),
                Kind = NavigationKind.Agent.ToLabel(),
                TypeName = string.Empty,
                Radius = is2D ? Default2DRadius : Default3DRadius,
                PathDesiredDistance = is2D ? Default2DPathDesiredDistance : Default3DPathDesiredDistance,
                TargetDesiredDistance = is2D ? Default2DTargetDesiredDistance : Default3DTargetDesiredDistance,
                MaxSpeed = is2D ? Default2DMaxSpeed : Default3DMaxSpeed,
                AvoidanceEnabled = false
            };
        }

        // ── Clamping rules reused by navigation-agent-configure ─────────────────────────────────────────────

        /// <summary>The smallest agent radius the clamp will yield (a tiny positive value, never 0).</summary>
        public const float MinRadius = 0.01f;

        /// <summary>Clamp an agent <c>radius</c> to a strictly-positive value (avoidance requires &gt; 0).</summary>
        public static float ClampRadius(float radius) =>
            (radius <= 0f || float.IsNaN(radius)) ? MinRadius : radius;

        /// <summary>Clamp a desired-distance (<c>path_desired_distance</c> / <c>target_desired_distance</c>) to non-negative.</summary>
        public static float ClampDistance(float distance) =>
            (distance < 0f || float.IsNaN(distance)) ? 0f : distance;

        /// <summary>Clamp an agent <c>max_speed</c> to a non-negative value.</summary>
        public static float ClampMaxSpeed(float maxSpeed) =>
            (maxSpeed < 0f || float.IsNaN(maxSpeed)) ? 0f : maxSpeed;
    }
}
