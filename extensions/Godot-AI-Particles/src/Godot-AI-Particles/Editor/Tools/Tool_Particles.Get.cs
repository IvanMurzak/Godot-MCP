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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Particles
{
    public partial class Tool_Particles
    {
        /// <summary>
        /// Editor-only, read-only tool — reads the scalar config of an existing
        /// <c>GpuParticles2D</c>/<c>GpuParticles3D</c> node. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "Particles / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read the scalar config (amount, lifetime, one-shot, emitting, explosiveness, randomness, " +
            "speed scale, preprocess, local coords) of an existing GpuParticles2D/GpuParticles3D node, " +
            "addressed by 'nodePath' (relative to the edited scene root). Read-only: does not modify the scene.")]
        public ParticlesNodeInfo Get
        (
            [Description("Node path (relative to the edited scene root) of the GpuParticles node to read.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() => ReadInfo(ResolveParticlesNodeOrThrow(nodePath)));
        }
    }
}
#endif
