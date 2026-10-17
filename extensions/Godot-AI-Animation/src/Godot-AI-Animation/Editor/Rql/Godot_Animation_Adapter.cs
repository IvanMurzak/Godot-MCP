/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System.Collections.Generic;
using Godot;
using Rql.Execution;
using GodotAnimation = Godot.Animation;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// Animation: validated <c>Length</c>, read-only <c>tracks</c> rows and <c>trackCount</c>; calls <c>AddTrack(kind, path)</c>,
    /// <c>InsertKey(track, time, value)</c> (value tracks) and <c>InsertTransformKey(track, time, "(x, y, z)")</c> (3D transform
    /// tracks; rotation in Euler degrees). Undoable: the calls change the animation itself.
    /// </summary>
    public sealed class Godot_Animation_Adapter : RqlTypeAdapter<GodotAnimation>
    {
        public override string Description =>
            "Animation (Animation extension): Length (seconds, validated), LoopMode, tracks (index kind path keyCount), trackCount; " +
            "call AddTrack(kind, path), InsertKey(track, time, value), InsertTransformKey(track, time, \"(x, y, z)\")";

        protected override void Members(MemberTable<GodotAnimation> m)
        {
            // By Godot property name: the C# Length is a float up to Godot 4.6 and a double from 4.7.
            m.Add<double>("Length", a => a.Get("length").AsDouble(), (a, v) => a.Set("length", v),
                    validate: (a, v) => AnimationRules.Length(v), description: "length in seconds (at least 0.001)")
             .Add<List<AnimationTrackRow>>("tracks", AnimationOps.Tracks, description: "the tracks (read-only rows: index kind path keyCount)")
             .Add<int>("trackCount", a => a.GetTrackCount(), description: "number of tracks");

            m.Untyped
                .AddMethod("AddTrack", new[] { new CallableParameter("kind", typeof(string)), new CallableParameter("path", typeof(string)) },
                    typeof(int), (o, a) => AnimationOps.AddTrack((GodotAnimation)o, (string)a[0]!, (string)a[1]!),
                    "add a track and return its index: kind value (path \"Node:property\", e.g. \"Mover:position:x\"), position-3d, rotation-3d " +
                    "or scale-3d (path \"Node\"); node paths are relative to the player's root node (its parent by default; \".\" = that node)")
                .AddMethod("InsertKey", new[] { new CallableParameter("track", typeof(int)), new CallableParameter("time", typeof(double)), new CallableParameter("value", typeof(Variant)) },
                    typeof(int), (o, a) => AnimationOps.InsertKey((GodotAnimation)o, (int)a[0]!, (double)a[1]!, (Variant)a[2]!),
                    "key a value track at a time (seconds) with a value (number, bool, text, resource …); returns the key index")
                .AddMethod("InsertTransformKey", new[] { new CallableParameter("track", typeof(int)), new CallableParameter("time", typeof(double)), new CallableParameter("value", typeof(Vector3)) },
                    typeof(int), (o, a) => AnimationOps.InsertTransformKey((GodotAnimation)o, (int)a[0]!, (double)a[1]!, (Vector3)a[2]!),
                    "key a position-3d / scale-3d track with \"(x, y, z)\", or a rotation-3d track with Euler angles in degrees; returns the key index");
        }
    }
}
#endif
