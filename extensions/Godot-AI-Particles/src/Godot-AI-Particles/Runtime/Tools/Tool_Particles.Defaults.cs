/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Particles
{
    public partial class Tool_Particles
    {
        /// <summary>
        /// Pure-managed tool — no Godot native API, so it lives OUTSIDE <c>#if TOOLS</c> and is fully
        /// CI-unit-testable (see <c>Tool_Particles_DefaultsTests</c>) and E2E-verifiable via
        /// <c>godot-cli run-tool particles-defaults</c>. Returns the recommended starter configuration for a
        /// 2D or 3D emitter, which the LLM can then pass to <c>particles-create</c> / <c>particles-configure</c>.
        /// </summary>
        [AiTool
        (
            DefaultsToolId,
            Title = "Particles / Defaults",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Return the recommended starter configuration (amount, lifetime, explosiveness, " +
            "speed scale, …) for a Godot GpuParticles emitter of the requested dimension. Pure-managed: " +
            "touches no scene, so it is safe to call any time to discover sane defaults before creating or " +
            "configuring a real emitter. 'dimension' accepts '2D' or '3D' (default '3D').")]
        public ParticlesNodeInfo Defaults
        (
            [Description("Particle dimension: '2D' (GpuParticles2D) or '3D' (GpuParticles3D). Defaults to '3D'.")]
            string? dimension = null
        )
        {
            var dim = string.IsNullOrWhiteSpace(dimension)
                ? ParticlesDimension.Three
                : ParticlesDimensions.Parse(dimension);
            return ParticlesDefaults.For(dim);
        }
    }
}
