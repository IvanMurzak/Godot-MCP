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
    /// The agent guide of the Phantom Camera RQL module, rendered into the <c>godot-rql</c> skill
    /// (<see cref="PhantomCameraModule"/>). Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses
    /// every example; the live e2e manifest (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene
    /// whose root is a Node3D.
    /// </summary>
    public static class PhantomCameraGuide
    {
        public const string Topic = "Phantom Camera";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "phantomcamera | select installed hint   is the addon installed (hint: how to install it)\n" +
            "root | call AddChild(\"Camera3D\", \"Camera3D\")   the scene camera the virtual cameras drive\n" +
            "godot.find(\"Camera3D\") | call AddChild(\"PhantomCameraHost\", \"PhantomCameraHost\")   required: without a host under the camera, phantom cameras do nothing\n" +
            "root | call AddChild(\"Node3D\", \"Player\")   something to follow and look at\n" +
            "root | call AddChild(\"PhantomCamera3D\", \"PCam\")   a virtual camera (PhantomCamera2D in a 2D scene)\n" +
            "phantomcamera.cameras | where name == \"PCam\" | call SetFollow(\"Simple\", \"Player\")   follow mode by name, then its target (validated)\n" +
            "phantomcamera.cameras | where name == \"PCam\" | call SetLookAt(\"Simple\", \"Player\")   look-at mode by name, then its target (3D only)\n" +
            "phantomcamera.cameras | where name == \"PCam\" | get node | set props[\"priority\"] = 50, props[\"follow_damping\"] = true   plain settings, through the node (undoable)\n" +
            "phantomcamera.cameras | select name scriptClass priority followMode followTarget lookAtMode lookAtTarget followDamping   the cameras and their settings\n" +
            "phantomcamera.hosts | select name path camera   the hosts and the camera each drives";

        public const string Markdown =
            "Phantom Camera (the ramokz/phantom-camera addon) adds Cinemachine-style virtual cameras whose classes are GDScript " +
            "class_names, so they have no C# type and `class` shows their native base (Node3D / Node2D). Create them with the " +
            "core `AddChild` by class name; the `phantomcamera` source lists them as read-only rows that name the class " +
            "(`scriptClass`), show the modes by name and hold the node itself in `node`. Read through the rows; write plain " +
            "settings through `get node` (`set props[\"…\"]`, undoable, marks the scene unsaved). The modes have validated " +
            "calls on the row: `SetFollow(mode, target?)` and `SetLookAt(mode, target?)` take the mode by name and set the " +
            "target after it (the addon ignores a target while the mode is None); they are NOT undoable (they mark the scene " +
            "unsaved). An unknown mode, ThirdPerson or a look-at mode on a PhantomCamera2D, a single target for None / Group, or " +
            "a target of the wrong dimension fails and changes nothing.\n\n```\n" + Examples + "\n```\n\n" +
            "A scene needs one PhantomCameraHost under its Camera3D (Camera2D in 2D); the highest `priority` camera (at least 0, " +
            "the addon clamps lower values) drives it. Follow modes: None, Glued, Simple, Group (props[\"follow_targets\"], a " +
            "list), Path (props[\"follow_path\"], a Path3D), Framed, ThirdPerson (3D). Look-at modes (3D): None, Mimic, Simple, " +
            "Group (props[\"look_at_targets\"]). Typical starting values: priority 10, follow mode Simple, look-at mode Simple, " +
            "damping on (props[\"follow_damping\"], props[\"look_at_damping\"]). Other settings are Godot properties of the " +
            "camera node (`props[\"follow_offset\"]`, `props[\"tween_resource\"]` …). " +
            "Without the addon `phantomcamera | select installed hint` says so and the lists are empty.";
    }
}
