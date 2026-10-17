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
    /// <summary>
    /// The members both emitter adapters share. GPUParticles3D and GPUParticles2D have no common emitter base class, so the
    /// validated members read and write the Godot properties by name (<c>amount</c>, <c>lifetime</c> …); they replace the
    /// C# members of the same name, keep their type, and add the <see cref="ParticlesRules"/> check.
    /// </summary>
    internal static class ParticlesMembers
    {
        public static void Configure<T>(MemberTable<T> m) where T : GodotObject
        {
            m.Add<int>("Amount", p => p.Get("amount").AsInt32(), (p, v) => p.Set("amount", v),
                    validate: (p, v) => ParticlesRules.Amount(v), description: "particles alive at once (at least 1)")
             .Add<double>("Lifetime", p => p.Get("lifetime").AsDouble(), (p, v) => p.Set("lifetime", v),
                    validate: (p, v) => ParticlesRules.Lifetime(v), description: "seconds each particle lives (above 0)")
             .Add<float>("Explosiveness", p => p.Get("explosiveness").AsSingle(), (p, v) => p.Set("explosiveness", v),
                    validate: (p, v) => ParticlesRules.Ratio("Explosiveness", v), description: "0 = steady stream, 1 = all particles at once")
             .Add<float>("Randomness", p => p.Get("randomness").AsSingle(), (p, v) => p.Set("randomness", v),
                    validate: (p, v) => ParticlesRules.Ratio("Randomness", v), description: "emission randomness ratio (0 to 1)")
             .Expose("Restart", "restart the emission from the beginning (clears the particles alive now)");
        }
    }
}
#endif
