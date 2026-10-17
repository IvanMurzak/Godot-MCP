/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// The agent guide of the Animation RQL module, rendered into the <c>godot-rql</c> skill (<see cref="AnimationModule"/>).
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e
    /// manifest (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene whose root is a Node3D.
    /// </summary>
    public static class AnimationGuide
    {
        public const string Topic = "Animation";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "root | call AddChild(\"Node3D\", \"Mover\")   the node to animate\n" +
            "root | call AddChild(\"AnimationPlayer\", \"Player\")   a player; track paths are relative to its parent (Root)\n" +
            "nodes | where name == \"Player\" | call AddLibrary(\"\")   the global library (\"\"); named ones too: AddLibrary(\"Extra\")\n" +
            "nodes | where name == \"Player\" | get libraries[\"\"] | call CreateAnimation(\"Idle\", 2, \"linear\")   an animation: name, length (s), loop mode none / linear / pingpong\n" +
            "nodes | where name == \"Player\" | call CreateAnimation(\"Walk\", 1, \"none\", \"Extra\")   on the player: the library is added when missing (animation \"Extra/Walk\")\n" +
            "nodes | where name == \"Player\" | get animations[\"Idle\"] | call AddTrack(\"value\", \"Mover:position:x\")   a value track \"node:property\"; returns its index\n" +
            "nodes | where name == \"Player\" | get animations[\"Idle\"] | call InsertKey(0, 0.5, 1.5)   key track 0 at 0.5 s\n" +
            "nodes | where name == \"Player\" | get animations[\"Idle\"] | call AddTrack(\"rotation-3d\", \"Mover\")   a 3D transform track: position-3d, rotation-3d or scale-3d\n" +
            "nodes | where name == \"Player\" | get animations[\"Idle\"] | call InsertTransformKey(1, 1, \"(0, 90, 0)\")   key it: a position / scale, or a rotation in Euler degrees\n" +
            "nodes | where name == \"Player\" | get animations[\"Idle\"] | set Length = 1.5, LoopMode = \"Pingpong\"   animation settings (Length validated)\n" +
            "nodes | where name == \"Player\" | get animations[\"Idle\"].tracks | select index kind path keyCount   read the tracks\n" +
            "nodes | where $ is AnimationPlayer | select name animations.keys() libraries.keys()   the players, their animations and libraries";

        public const string Markdown =
            "Animations live in the AnimationLibrary resources of an AnimationPlayer (or AnimationTree): `libraries[\"name\"]` " +
            "(\"\" = the global library) and `animations[\"name\"]` (qualified: \"Idle\" in the global library, \"Extra/Walk\" in " +
            "library Extra) reach them. Add the player with `AddChild`, then:\n\n```\n" + Examples + "\n```\n\n" +
            "`AddTrack` kinds: `value` (path \"node:property\", e.g. \"Mover:modulate\" or \"Mover:position:x\"), `position-3d`, " +
            "`rotation-3d`, `scale-3d` (path \"node\"); node paths are relative to the player's root node (its parent by default, " +
            "\".\" = that node). `InsertKey` keys value tracks (a number, bool, text or resource); `InsertTransformKey` keys the " +
            "transform tracks with \"(x, y, z)\". Every change is undoable (it changes the animation or library it is called on) " +
            "and marks the scene unsaved, except `CreateAnimation` on the player into a library that already exists: call it on " +
            "the library (`get libraries[\"…\"] | call CreateAnimation(…)`) to undo it. Typical values: length 1 s, loop mode none. " +
            "`Length` (at least 0.001 s), key times (0 or more), track indexes and names (no '/', ':', ',' or '[') are validated: a " +
            "rejected value changes nothing. Other settings are C# members (`LoopMode`, `Step` …) or " +
            "`props[\"…\"]`; `rql-schema \"Animation\"` lists them.";
    }
}
