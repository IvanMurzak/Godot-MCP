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
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for the recommended starter
    /// configuration of a Phantom Camera virtual camera. The pure-managed <c>phantomcamera-defaults</c> tool
    /// returns this so an LLM can discover sane values before creating/configuring a real PhantomCamera —
    /// without the addon installed and without touching a scene. Mirrors <c>ParticlesDefaults</c> in
    /// Godot-AI-Particles.
    /// </summary>
    public static class PhantomCameraDefaults
    {
        /// <summary>Recommended starter <c>priority</c> (a mid value so a new camera can win or be overridden).</summary>
        public const int DefaultPriority = 10;

        /// <summary>Recommended starter follow mode — <see cref="PhantomFollowMode.Simple"/>.</summary>
        public const PhantomFollowMode DefaultFollowMode = PhantomFollowMode.Simple;

        /// <summary>Recommended starter look-at mode — <see cref="PhantomLookAtMode.Simple"/>.</summary>
        public const PhantomLookAtMode DefaultLookAtMode = PhantomLookAtMode.Simple;

        /// <summary>Recommended starter <c>follow_damping</c> (smooth, Cinemachine-like motion).</summary>
        public const bool DefaultFollowDamping = true;

        /// <summary>Recommended starter <c>look_at_damping</c>.</summary>
        public const bool DefaultLookAtDamping = true;

        /// <summary>
        /// A recommended starter configuration, as a fully-populated <see cref="PhantomCameraInfo"/> with no
        /// bound node (<see cref="PhantomCameraInfo.NodePath"/> empty). <paramref name="dimension"/> selects the
        /// reported <see cref="PhantomCameraInfo.TypeName"/> (<c>PhantomCamera2D</c> for "2D", else
        /// <c>PhantomCamera3D</c>); the scalar starter values are the same for both.
        /// </summary>
        public static PhantomCameraInfo For(string? dimension = null)
        {
            var is2D = dimension != null
                && (dimension.Trim().ToLowerInvariant() is "2d" or "2" or "two");

            return new PhantomCameraInfo
            {
                Installed = true,
                Addon = AddonGate.PhantomCameraAddon,
                NodePath = string.Empty,
                TypeName = is2D ? PhantomCameraNames.PhantomCamera2D : PhantomCameraNames.PhantomCamera3D,
                CameraPath = string.Empty,
                Priority = DefaultPriority,
                FollowMode = DefaultFollowMode.ToLabel(),
                FollowTarget = string.Empty,
                LookAtMode = DefaultLookAtMode.ToLabel(),
                LookAtTarget = string.Empty,
                FollowDamping = DefaultFollowDamping,
                LookAtDamping = DefaultLookAtDamping
            };
        }
    }
}
