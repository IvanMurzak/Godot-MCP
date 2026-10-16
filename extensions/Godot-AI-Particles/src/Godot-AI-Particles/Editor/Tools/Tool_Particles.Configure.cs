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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Particles
{
    public partial class Tool_Particles
    {
        /// <summary>
        /// Editor-only tool — updates scalar properties of an existing <c>GpuParticles2D</c>/<c>GpuParticles3D</c>
        /// node. Only the parameters you pass are changed; each is clamped to a valid range (pure-managed
        /// <see cref="ParticlesDefaults"/> rules) before it touches the node. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            ConfigureToolId,
            Title = "Particles / Configure"
        )]
        [Description("Update scalar properties of an existing GpuParticles2D/GpuParticles3D node, addressed by " +
            "'nodePath' (relative to the edited scene root). Only the arguments you supply are changed; each is " +
            "clamped to a valid range (amount >= 1, lifetime > 0, explosiveness/randomness in 0..1, speed scale " +
            "and preprocess >= 0). Returns the node's updated config.")]
        public ParticlesNodeInfo Configure
        (
            [Description("Node path (relative to the edited scene root) of the GpuParticles node to configure.")]
            string nodePath,
            [Description("New particle count (Godot 'amount'); clamped to >= 1.")]
            int? amount = null,
            [Description("New particle lifetime in seconds (Godot 'lifetime'); clamped to > 0.")]
            double? lifetime = null,
            [Description("New one-shot flag (Godot 'one_shot'): emit a single burst then stop.")]
            bool? oneShot = null,
            [Description("New emission burst ratio (Godot 'explosiveness'); clamped to 0..1.")]
            float? explosiveness = null,
            [Description("New emission timing randomness (Godot 'randomness'); clamped to 0..1.")]
            float? randomness = null,
            [Description("New simulation speed multiplier (Godot 'speed_scale'); clamped to >= 0.")]
            double? speedScale = null,
            [Description("New pre-simulation seconds applied on start (Godot 'preprocess'); clamped to >= 0.")]
            double? preprocess = null,
            [Description("New local-space simulation flag (Godot 'local_coords').")]
            bool? localCoords = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var node = ResolveParticlesNodeOrThrow(nodePath);
                ApplyConfig(node, amount, lifetime, oneShot, explosiveness, randomness,
                    speedScale, preprocess, localCoords);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(node);
            });
        }
    }
}
#endif
