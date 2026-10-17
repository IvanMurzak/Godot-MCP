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
    /// <summary>
    /// AnimationMixer (AnimationPlayer, AnimationTree): the <c>libraries</c> and <c>animations</c> maps (read-only; keys are
    /// library names and qualified animation names), <c>AddLibrary(name)</c> and <c>CreateAnimation(…)</c>.
    /// </summary>
    public sealed class Godot_AnimationMixer_Adapter : RqlTypeAdapter<AnimationMixer>
    {
        public override string Description =>
            "AnimationPlayer / AnimationTree (Animation extension): libraries[\"name\"] and animations[\"library/name\"] (\"\" = the global library); " +
            "call AddLibrary(name), CreateAnimation(name, length, loopMode, library)";

        protected override void Members(MemberTable<AnimationMixer> m)
        {
            m.Map<AnimationLibrary>("libraries", AnimationOps.LibraryNames, (p, k) => p.GetAnimationLibrary(k),
                    has: (p, k) => p.HasAnimationLibrary(k),
                    description: "the animation libraries by name (\"\" = the global library); read-only, add one with call AddLibrary(name)")
             .Map<GodotAnimation>("animations", AnimationOps.AnimationNames, (p, k) => p.GetAnimation(k),
                    has: (p, k) => p.HasAnimation(k),
                    description: "every animation by qualified name (\"Idle\" in the global library, \"Extra/Walk\" in library Extra); read-only");

            m.Untyped
                .AddMethod("AddLibrary", new[] { new CallableParameter("name", typeof(string)) }, typeof(AnimationLibrary),
                    (o, a) => AnimationOps.AddLibrary((AnimationMixer)o, (string)a[0]!),
                    "add an empty animation library (\"\" = the global library) and return it (undoable)")
                .AddMethod("CreateAnimation", new[]
                    {
                        new CallableParameter("name", typeof(string)),
                        new CallableParameter("length", typeof(double), hasDefault: true, defaultValue: 1.0),
                        new CallableParameter("loopMode", typeof(string), hasDefault: true, defaultValue: "none"),
                        new CallableParameter("library", typeof(string), hasDefault: true, defaultValue: ""),
                    }, typeof(GodotAnimation),
                    (o, a) => AnimationOps.CreateAnimation((AnimationMixer)o, (string)a[0]!, (double)a[1]!, a[2] as string, (string?)a[3] ?? ""),
                    "create an animation (length in seconds, loopMode none / linear / pingpong) in a library, adding the library when it is missing; " +
                    "returns it. Undoable only when it adds the library: in an existing library call CreateAnimation on the library itself");
        }
    }
}
#endif
