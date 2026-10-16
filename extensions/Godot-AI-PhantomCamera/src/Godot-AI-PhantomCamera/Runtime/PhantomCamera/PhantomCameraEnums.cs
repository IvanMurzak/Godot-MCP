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

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// <b>Class B (addon-dependent) contract.</b> The community <c>Phantom Camera</c> addon is a GDScript
    /// <c>class_name</c> addon whose types are NOT in GodotSharp, so this extension references its classes +
    /// members ONLY by string name and drives its enums as plain ints. There are therefore no compile-time
    /// enum types to lean on — <b>these pure-managed constants ARE the contract</b>, pinned by unit tests
    /// (<c>PhantomCameraEnumsTests</c>) so a silent drift from the real addon turns a test red.
    ///
    /// <para>
    /// Values verified against <c>ramokz/phantom-camera</c> <b>v0.11.0.2</b>
    /// (<c>addons/phantom_camera/scripts/phantom_camera/phantom_camera_3d.gd</c>).
    /// </para>
    /// </summary>
    public enum PhantomFollowMode
    {
        /// <summary><c>FollowMode.NONE</c> — no follow logic.</summary>
        None = 0,
        /// <summary><c>FollowMode.GLUED</c> — sticks to its target.</summary>
        Glued = 1,
        /// <summary><c>FollowMode.SIMPLE</c> — follows its target with an optional offset.</summary>
        Simple = 2,
        /// <summary><c>FollowMode.GROUP</c> — follows multiple targets, can reframe itself.</summary>
        Group = 3,
        /// <summary><c>FollowMode.PATH</c> — follows a target confined to a <c>Path3D</c>.</summary>
        Path = 4,
        /// <summary><c>FollowMode.FRAMED</c> — dead-zone follow; only moves when the target leaves the frame.</summary>
        Framed = 5,
        /// <summary><c>FollowMode.THIRD_PERSON</c> — <c>SpringArm3D</c> follow that can rotate around the target.</summary>
        ThirdPerson = 6
    }

    /// <summary>
    /// The addon's <c>LookAtMode</c> enum (int) — see <see cref="PhantomFollowMode"/> for the Class-B
    /// "constants ARE the contract" rationale. Verified against Phantom Camera v0.11.0.2.
    /// </summary>
    public enum PhantomLookAtMode
    {
        /// <summary><c>LookAtMode.NONE</c> — no look-at logic.</summary>
        None = 0,
        /// <summary><c>LookAtMode.MIMIC</c> — copies its target's rotation.</summary>
        Mimic = 1,
        /// <summary><c>LookAtMode.SIMPLE</c> — looks at its target in a straight line.</summary>
        Simple = 2,
        /// <summary><c>LookAtMode.GROUP</c> — looks at the centre of its targets.</summary>
        Group = 3
    }

    /// <summary>
    /// The Phantom Camera addon's <b>class names</b> (GDScript <c>class_name</c> types, registered in the
    /// global script-class list — NOT in <c>ClassDB</c>) and its <b>member names</b> (GDScript
    /// <c>snake_case</c>, NOT C# <c>PascalCase</c>). Every dynamic <c>Set</c>/<c>Get</c>/<c>Call</c> the editor
    /// tools issue goes through one of these constants, so the snake_case spelling is pinned in ONE place and
    /// unit-tested.
    /// </summary>
    public static class PhantomCameraNames
    {
        // ── class names (resolved via the global script-class list) ───────────────────────────────
        /// <summary>The 3D virtual-camera class (also the presence-gate probe class).</summary>
        public const string PhantomCamera3D = "PhantomCamera3D";
        /// <summary>The 2D virtual-camera class.</summary>
        public const string PhantomCamera2D = "PhantomCamera2D";
        /// <summary>The companion host that actually drives the real <c>Camera3D</c>/<c>Camera2D</c>.</summary>
        public const string PhantomCameraHost = "PhantomCameraHost";

        // ── member names (GDScript snake_case) ────────────────────────────────────────────────────
        /// <summary><c>priority</c> (int) — higher wins when multiple PhantomCameras are active.</summary>
        public const string Priority = "priority";
        /// <summary><c>follow_mode</c> (int enum, <see cref="PhantomFollowMode"/>).</summary>
        public const string FollowMode = "follow_mode";
        /// <summary><c>follow_target</c> (Node3D).</summary>
        public const string FollowTarget = "follow_target";
        /// <summary><c>look_at_mode</c> (int enum, <see cref="PhantomLookAtMode"/>).</summary>
        public const string LookAtMode = "look_at_mode";
        /// <summary><c>look_at_target</c> (Node3D).</summary>
        public const string LookAtTarget = "look_at_target";
        /// <summary><c>follow_damping</c> (bool).</summary>
        public const string FollowDamping = "follow_damping";
        /// <summary><c>follow_damping_value</c> (Vector3).</summary>
        public const string FollowDampingValue = "follow_damping_value";
        /// <summary><c>look_at_damping</c> (bool).</summary>
        public const string LookAtDamping = "look_at_damping";
        /// <summary><c>look_at_damping_value</c> (float).</summary>
        public const string LookAtDampingValue = "look_at_damping_value";
    }

    /// <summary>
    /// Pure-managed parse/label helpers for the addon's two enums. CI-unit-testable (no Godot binary), so the
    /// string ⇄ int mapping the editor tools rely on is verified without a live editor.
    /// </summary>
    public static class PhantomCameraModes
    {
        /// <summary>The int value the addon expects (a plain GDScript enum int).</summary>
        public static int ToInt(this PhantomFollowMode mode) => (int)mode;

        /// <summary>The int value the addon expects (a plain GDScript enum int).</summary>
        public static int ToInt(this PhantomLookAtMode mode) => (int)mode;

        /// <summary>The display label returned in tool results (e.g. <c>"Simple"</c>, <c>"ThirdPerson"</c>).</summary>
        public static string ToLabel(this PhantomFollowMode mode) => mode.ToString();

        /// <summary>The display label returned in tool results (e.g. <c>"Mimic"</c>, <c>"Group"</c>).</summary>
        public static string ToLabel(this PhantomLookAtMode mode) => mode.ToString();

        /// <summary>
        /// Parse a user/LLM-supplied follow-mode string. Accepts (case- and whitespace-insensitive) the mode
        /// name (<c>none/glued/simple/group/path/framed/third_person</c>, with <c>thirdperson</c> /
        /// <c>third-person</c> / <c>third person</c> aliases) or its int <c>0..6</c>.
        /// </summary>
        public static bool TryParseFollow(string? value, out PhantomFollowMode mode)
        {
            mode = PhantomFollowMode.None;
            if (string.IsNullOrWhiteSpace(value)) return false;
            switch (Normalize(value!))
            {
                case "none": case "0": mode = PhantomFollowMode.None; return true;
                case "glued": case "1": mode = PhantomFollowMode.Glued; return true;
                case "simple": case "2": mode = PhantomFollowMode.Simple; return true;
                case "group": case "3": mode = PhantomFollowMode.Group; return true;
                case "path": case "4": mode = PhantomFollowMode.Path; return true;
                case "framed": case "5": mode = PhantomFollowMode.Framed; return true;
                case "thirdperson": case "third_person": case "6": mode = PhantomFollowMode.ThirdPerson; return true;
                default: return false;
            }
        }

        /// <summary>Parse a follow-mode string, throwing <see cref="ArgumentException"/> on an unknown value.</summary>
        public static PhantomFollowMode ParseFollow(string? value) =>
            TryParseFollow(value, out var mode)
                ? mode
                : throw new ArgumentException(
                    $"Unrecognized follow mode '{value}'. Use None/Glued/Simple/Group/Path/Framed/ThirdPerson.", nameof(value));

        /// <summary>
        /// Parse a user/LLM-supplied look-at-mode string. Accepts (case- and whitespace-insensitive) the mode
        /// name (<c>none/mimic/simple/group</c>) or its int <c>0..3</c>.
        /// </summary>
        public static bool TryParseLookAt(string? value, out PhantomLookAtMode mode)
        {
            mode = PhantomLookAtMode.None;
            if (string.IsNullOrWhiteSpace(value)) return false;
            switch (Normalize(value!))
            {
                case "none": case "0": mode = PhantomLookAtMode.None; return true;
                case "mimic": case "1": mode = PhantomLookAtMode.Mimic; return true;
                case "simple": case "2": mode = PhantomLookAtMode.Simple; return true;
                case "group": case "3": mode = PhantomLookAtMode.Group; return true;
                default: return false;
            }
        }

        /// <summary>Parse a look-at-mode string, throwing <see cref="ArgumentException"/> on an unknown value.</summary>
        public static PhantomLookAtMode ParseLookAt(string? value) =>
            TryParseLookAt(value, out var mode)
                ? mode
                : throw new ArgumentException(
                    $"Unrecognized look-at mode '{value}'. Use None/Mimic/Simple/Group.", nameof(value));

        // Lower-cases, trims, and collapses '-'/' ' to '_' so "Third Person"/"third-person" == "third_person".
        static string Normalize(string value)
        {
            var lowered = value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            // Also accept the no-separator spelling ("thirdperson").
            return lowered;
        }
    }
}
