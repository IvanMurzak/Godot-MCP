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
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed: libraries / animations and their creation on AnimationMixer (AnimationPlayer, AnimationTree) and
    /// AnimationLibrary, tracks and keys on Animation, and the agent guide rendered into the <c>godot-rql</c> skill. Creating
    /// the player is the core <c>AddChild</c> call, so the extension has no MCP tools of its own.
    /// </summary>
    public sealed class AnimationModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddAdapter(new Godot_AnimationMixer_Adapter())
               .AddAdapter(new Godot_AnimationLibrary_Adapter())
               .AddAdapter(new Godot_Animation_Adapter())
               .AddGuide(AnimationGuide.Topic, AnimationGuide.Markdown);
    }
}
#endif
