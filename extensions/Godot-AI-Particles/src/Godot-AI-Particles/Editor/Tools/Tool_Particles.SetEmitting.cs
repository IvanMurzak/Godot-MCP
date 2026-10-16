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
        /// Editor-only tool — starts or stops emission on an existing <c>GpuParticles2D</c>/<c>GpuParticles3D</c>
        /// node (Godot <c>emitting</c>), optionally restarting the emission first. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            SetEmittingToolId,
            Title = "Particles / Set Emitting"
        )]
        [Description("Start or stop emission on an existing GpuParticles2D/GpuParticles3D node, addressed by " +
            "'nodePath' (relative to the edited scene root). 'emitting' true starts emission, false stops it. " +
            "Optionally pass 'restart' true to restart the emission from a clean state first (useful for " +
            "one-shot bursts). Returns the node's updated config.")]
        public ParticlesNodeInfo SetEmitting
        (
            [Description("Node path (relative to the edited scene root) of the GpuParticles node.")]
            string nodePath,
            [Description("True to start emitting, false to stop (Godot 'emitting').")]
            bool emitting,
            [Description("When true, restart the emission from a clean state before applying 'emitting'.")]
            bool restart = false
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var node = ResolveParticlesNodeOrThrow(nodePath);
                SetEmittingState(node, emitting, restart);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(node);
            });
        }
    }
}
#endif
