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

namespace com.IvanMurzak.Godot.MCP.Particles
{
    /// <summary>GPUParticles2D: validated Amount / Lifetime / Explosiveness / Randomness and <c>Restart()</c> (see <see cref="ParticlesMembers"/>).</summary>
    public sealed class Godot_GpuParticles2D_Adapter : RqlTypeAdapter<GpuParticles2D>
    {
        public override string Description =>
            "GPUParticles2D emitter (Particles extension): Amount >= 1, Lifetime > 0, Explosiveness / Randomness 0..1 are validated; call Restart()";

        protected override void Members(MemberTable<GpuParticles2D> m) => ParticlesMembers.Configure(m);
    }
}
#endif
