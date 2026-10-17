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
    /// The Phantom Camera addon's names, the contract of the module: its classes are GDScript <c>class_name</c>s (no C#
    /// type, not in ClassDB), so they are referenced by string only. Verified against <c>ramokz/phantom-camera</c>
    /// v0.11.0.2. Pure-managed (no Godot API), so it is unit-tested.
    /// </summary>
    public static class PhantomCameraNames
    {
        public const string AddonName = "Phantom Camera";

        public const string PhantomCamera3D = "PhantomCamera3D";
        public const string PhantomCamera2D = "PhantomCamera2D";
        public const string PhantomCameraHost = "PhantomCameraHost";

        // GDScript properties (snake_case, read / written with props["…"]).
        public const string Priority = "priority";
        public const string FollowMode = "follow_mode";
        public const string FollowTarget = "follow_target";
        public const string LookAtMode = "look_at_mode";
        public const string LookAtTarget = "look_at_target";
        public const string FollowDamping = "follow_damping";
        public const string LookAtDamping = "look_at_damping";

        public const string InstallHint =
            "Phantom Camera is not installed: install 'Phantom Camera' from the Godot Asset Library (or ramokz/phantom-camera " +
            "v0.11.0.2 into res://addons/phantom_camera/), enable it under Project Settings -> Plugins so its " +
            "PhantomCameraManager autoload is registered, then rebuild.";
    }
}
