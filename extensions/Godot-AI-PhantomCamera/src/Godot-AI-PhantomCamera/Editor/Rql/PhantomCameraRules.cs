/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// The checks of the camera rows' <c>SetFollow</c> / <c>SetLookAt</c>, run before anything is written. Each returns the
    /// rejection reason, or null when the value is valid. The addon's own setters ignore a target silently when the mode
    /// takes none (None, Group), which is why those combinations are rejected. Pure-managed (no Godot API), so it is
    /// unit-tested.
    /// </summary>
    public static class PhantomCameraRules
    {
        public static string? FollowMode(string? name, bool is2D, out PhantomFollowMode mode)
        {
            if (!PhantomCameraModes.TryParse(name, out mode))
                return $"unknown follow mode '{name}': use None, Glued, Simple, Group, Path, Framed{(is2D ? "" : " or ThirdPerson")}";
            if (is2D && mode == PhantomFollowMode.ThirdPerson)
                return "ThirdPerson is a PhantomCamera3D follow mode; a PhantomCamera2D has None, Glued, Simple, Group, Path, Framed";
            return null;
        }

        public static string? LookAtMode(string? name, bool is2D, out PhantomLookAtMode mode)
        {
            mode = PhantomLookAtMode.None;
            if (is2D) return "a PhantomCamera2D has no look-at mode (only a PhantomCamera3D does)";
            return PhantomCameraModes.TryParse(name, out mode)
                ? null
                : $"unknown look-at mode '{name}': use None, Mimic, Simple or Group";
        }

        /// <summary>A single follow target only applies to the modes that follow one node.</summary>
        public static string? FollowTarget(PhantomFollowMode mode) => mode switch
        {
            PhantomFollowMode.None => "follow mode None follows nothing: omit the target",
            PhantomFollowMode.Group => "follow mode Group follows the list props[\"follow_targets\"], not one target: omit the target and set that list",
            _ => null,
        };

        /// <summary>A single look-at target only applies to the modes that look at one node.</summary>
        public static string? LookAtTarget(PhantomLookAtMode mode) => mode switch
        {
            PhantomLookAtMode.None => "look-at mode None looks at nothing: omit the target",
            PhantomLookAtMode.Group => "look-at mode Group looks at the list props[\"look_at_targets\"], not one target: omit the target and set that list",
            _ => null,
        };

        /// <summary>A 3D camera follows / looks at a Node3D, a 2D camera a Node2D.</summary>
        public static string? TargetType(bool is2D, bool targetMatches, string targetName, string targetClass) =>
            targetMatches ? null : $"target '{targetName}' is a {targetClass}; a {(is2D ? "PhantomCamera2D needs a Node2D" : "PhantomCamera3D needs a Node3D")}";
    }
}
