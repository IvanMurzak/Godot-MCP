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
    /// Pure-managed, serializable snapshot returned by every <c>phantomcamera-*</c> tool. Holds ONLY
    /// primitives + strings (no Godot native types), so it is safe to build inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate and return across the tool boundary, it serializes cleanly
    /// through ReflectorNet, and the pure-managed <c>phantomcamera-defaults</c> tool can produce one with no
    /// Godot binary (CI-unit-testable).
    ///
    /// <para>
    /// <b>Class-B presence gate (Option A).</b> The first four fields ARE the gate: when the Phantom Camera
    /// addon is absent a tool returns <see cref="NotInstalled"/> (<see cref="Installed"/> = <c>false</c> +
    /// <see cref="Hint"/>) instead of throwing. When the addon is present, <see cref="Installed"/> is
    /// <c>true</c> and the camera-config fields below are populated. The e2e fixture asserts
    /// <c>"Installed":true</c> on the happy path.
    /// </para>
    /// </summary>
    public sealed class PhantomCameraInfo
    {
        // ── Class-B presence gate (Option A) ──────────────────────────────────────────────────────
        /// <summary>Whether the Phantom Camera addon was detected (gate). False ⇒ the config fields are unset.</summary>
        public bool Installed { get; set; } = true;

        /// <summary>Display name of the wrapped addon.</summary>
        public string Addon { get; set; } = AddonGate.PhantomCameraAddon;

        /// <summary>When not installed, the class the gate probed for (e.g. <c>PhantomCamera3D</c>); else null.</summary>
        public string? MissingClass { get; set; }

        /// <summary>When not installed, an actionable install hint; else empty.</summary>
        public string Hint { get; set; } = string.Empty;

        // ── Camera config (valid only when Installed == true) ─────────────────────────────────────
        /// <summary>Scene path of the node (empty for a defaults snapshot not bound to a node).</summary>
        public string NodePath { get; set; } = string.Empty;

        /// <summary>The wrapped class name (e.g. <c>PhantomCamera3D</c> / <c>PhantomCameraHost</c>).</summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>For <c>phantomcamera-host-create</c>: the <c>Camera3D</c> the host was attached to.</summary>
        public string CameraPath { get; set; } = string.Empty;

        /// <summary>Phantom Camera <c>priority</c> — higher wins when multiple cameras are active.</summary>
        public int Priority { get; set; }

        /// <summary>Follow-mode label (see <see cref="PhantomFollowMode"/>; e.g. <c>"Simple"</c>).</summary>
        public string FollowMode { get; set; } = string.Empty;

        /// <summary>Scene path of the follow target, or empty when none is set.</summary>
        public string FollowTarget { get; set; } = string.Empty;

        /// <summary>Look-at-mode label (see <see cref="PhantomLookAtMode"/>; e.g. <c>"Mimic"</c>).</summary>
        public string LookAtMode { get; set; } = string.Empty;

        /// <summary>Scene path of the look-at target, or empty when none is set.</summary>
        public string LookAtTarget { get; set; } = string.Empty;

        /// <summary>Phantom Camera <c>follow_damping</c> flag.</summary>
        public bool FollowDamping { get; set; }

        /// <summary>Phantom Camera <c>look_at_damping</c> flag.</summary>
        public bool LookAtDamping { get; set; }

        /// <summary>
        /// The structured "Phantom Camera addon not installed" result (Option A presence gate). Single-sources
        /// its fields from <see cref="AddonGate"/> so the addon name, probe class, and hint never drift.
        /// </summary>
        public static PhantomCameraInfo NotInstalled() => new()
        {
            Installed = false,
            Addon = AddonGate.PhantomCameraAddon,
            MissingClass = AddonGate.PresenceClass,
            Hint = AddonGate.InstallHint
        };
    }
}
