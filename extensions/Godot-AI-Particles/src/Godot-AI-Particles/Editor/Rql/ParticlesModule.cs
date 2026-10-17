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

namespace com.IvanMurzak.Godot.MCP.Particles
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed: validated emitter members on GPUParticles3D / GPUParticles2D, <c>Restart()</c>, and the agent guide
    /// rendered into the <c>godot-rql</c> skill. Creating, reading and writing emitters are ordinary RQL (the core
    /// <c>AddChild</c> call, <c>set</c>, <c>select</c>), so the extension has no MCP tools of its own.
    /// </summary>
    public sealed class ParticlesModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddAdapter(new Godot_GpuParticles3D_Adapter())
               .AddAdapter(new Godot_GpuParticles2D_Adapter())
               .AddGuide(ParticlesGuide.Topic, ParticlesGuide.Markdown);
    }
}
#endif
