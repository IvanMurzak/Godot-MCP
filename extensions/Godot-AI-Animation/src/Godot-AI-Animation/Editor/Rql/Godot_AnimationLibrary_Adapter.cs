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
using Godot;
using Rql.Execution;
using GodotAnimation = Godot.Animation;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>AnimationLibrary: the <c>animations</c> map (read-only, by name) and <c>CreateAnimation(…)</c> (undoable: it changes the library).</summary>
    public sealed class Godot_AnimationLibrary_Adapter : RqlTypeAdapter<AnimationLibrary>
    {
        public override string Description =>
            "AnimationLibrary (Animation extension): animations[\"name\"]; call CreateAnimation(name, length, loopMode)";

        protected override void Members(MemberTable<AnimationLibrary> m)
        {
            m.Map<GodotAnimation>("animations", AnimationOps.AnimationNames, (l, k) => l.GetAnimation(k),
                has: (l, k) => l.HasAnimation(k), description: "the library's animations by name; read-only, add one with call CreateAnimation(name)");

            m.Untyped.AddMethod("CreateAnimation", new[]
                {
                    new CallableParameter("name", typeof(string)),
                    new CallableParameter("length", typeof(double), hasDefault: true, defaultValue: 1.0),
                    new CallableParameter("loopMode", typeof(string), hasDefault: true, defaultValue: "none"),
                }, typeof(GodotAnimation),
                (o, a) => AnimationOps.CreateAnimation((AnimationLibrary)o, (string)a[0]!, (double)a[1]!, a[2] as string),
                "create an animation in this library (length in seconds, loopMode none / linear / pingpong) and return it (undoable)");
        }
    }
}
#endif
