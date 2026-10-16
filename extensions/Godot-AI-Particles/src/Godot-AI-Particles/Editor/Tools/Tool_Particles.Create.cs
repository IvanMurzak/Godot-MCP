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
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Particles
{
    public partial class Tool_Particles
    {
        /// <summary>
        /// Editor-only tool — creates a <c>GpuParticles2D</c> or <c>GpuParticles3D</c> node in the currently
        /// edited scene and returns its structured config. All Godot API access is marshalled onto the editor
        /// main thread via <c>MainThread.Instance.Run(...)</c>.
        /// </summary>
        [AiTool
        (
            CreateToolId,
            Title = "Particles / Create"
        )]
        [Description("Create a GpuParticles emitter in the currently edited Godot scene and return its " +
            "structured config. 'dimension' is '2D' (GpuParticles2D) or '3D' (GpuParticles3D), default '3D'. " +
            "Optionally pass 'parentPath' (a node path relative to the scene root) to parent it (defaults to " +
            "the scene root), 'name' to rename it, and 'amount'/'lifetime' to seed its config (both clamped to " +
            "valid ranges). The new node's owner is set to the scene root so it is saved with the scene.")]
        public ParticlesNodeInfo Create
        (
            [Description("Particle dimension: '2D' (GpuParticles2D) or '3D' (GpuParticles3D). Defaults to '3D'.")]
            string? dimension = null,
            [Description("Name for the new node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the node " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("Optional initial particle count (Godot 'amount'); clamped to >= 1.")]
            int? amount = null,
            [Description("Optional initial particle lifetime in seconds (Godot 'lifetime'); clamped to > 0.")]
            double? lifetime = null
        )
        {
            var dim = string.IsNullOrWhiteSpace(dimension)
                ? ParticlesDimension.Three
                : ParticlesDimensions.Parse(dimension);

            return MainThread.Instance.Run(() =>
            {
                var root = GetEditedSceneRootOrThrow();

                Node parent = root;
                if (!string.IsNullOrWhiteSpace(parentPath))
                    parent = root.GetNodeOrNull(new NodePath(parentPath))
                        ?? throw new ArgumentException($"No parent node found at path '{parentPath}'.", nameof(parentPath));

                Node node = dim == ParticlesDimension.Two
                    ? new GpuParticles2D()
                    : new GpuParticles3D();

                if (!string.IsNullOrWhiteSpace(name))
                    node.Name = name;

                ApplyConfig(node, amount, lifetime,
                    oneShot: null, explosiveness: null, randomness: null,
                    speedScale: null, preprocess: null, localCoords: null);

                parent.AddChild(node);
                node.Owner = root; // so the node is persisted when the scene is saved

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(node);

                return ReadInfo(node);
            });
        }
    }
}
#endif
