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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Particles
{
    /// <summary>
    /// Editor-only shared helpers for the <c>particles-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>s). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor main thread.
    ///
    /// <para>
    /// The reads/writes use the strongly-typed <c>GpuParticles2D</c> / <c>GpuParticles3D</c> API on purpose —
    /// that typed surface (resolved from the consumer's own GodotSharp) is exactly what the source-only
    /// packaging recipe must compile against cross-version. Property names are identical on both node types
    /// and stable across Godot 4.3 … 4.5, so one info shape (<see cref="ParticlesNodeInfo"/>) covers both.
    /// </para>
    /// </summary>
    public partial class Tool_Particles
    {
        /// <summary>The edited scene root, or throw a clear error when no scene is open.</summary>
        static Node GetEditedSceneRootOrThrow()
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
                throw new InvalidOperationException(
                    "No scene is currently being edited; open or create a scene first.");
            return root;
        }

        /// <summary>
        /// Resolve <paramref name="nodePath"/> (relative to the edited scene root) to a particle node, throwing
        /// a clear error when the path is empty, the node is missing, or the node is not a
        /// <c>GpuParticles2D</c>/<c>GpuParticles3D</c>.
        /// </summary>
        static Node ResolveParticlesNodeOrThrow(string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("A node path is required.", nameof(nodePath));

            var root = GetEditedSceneRootOrThrow();
            var node = root.GetNodeOrNull(new NodePath(nodePath));
            if (node == null)
                throw new ArgumentException($"No node found at path '{nodePath}'.", nameof(nodePath));

            if (node is not GpuParticles2D && node is not GpuParticles3D)
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not a GpuParticles2D/GpuParticles3D.",
                    nameof(nodePath));

            return node;
        }

        /// <summary>Build a pure-managed <see cref="ParticlesNodeInfo"/> snapshot from a live particle node.</summary>
        static ParticlesNodeInfo ReadInfo(Node node)
        {
            var path = node.GetPath().ToString();
            if (node is GpuParticles3D p3)
                return new ParticlesNodeInfo
                {
                    NodePath = path,
                    Dimension = ParticlesDimension.Three.ToLabel(),
                    TypeName = node.GetClass(),
                    Amount = p3.Amount,
                    Lifetime = p3.Lifetime,
                    OneShot = p3.OneShot,
                    Emitting = p3.Emitting,
                    Explosiveness = p3.Explosiveness,
                    Randomness = p3.Randomness,
                    SpeedScale = p3.SpeedScale,
                    Preprocess = p3.Preprocess,
                    LocalCoords = p3.LocalCoords
                };

            var p2 = (GpuParticles2D)node;
            return new ParticlesNodeInfo
            {
                NodePath = path,
                Dimension = ParticlesDimension.Two.ToLabel(),
                TypeName = node.GetClass(),
                Amount = p2.Amount,
                Lifetime = p2.Lifetime,
                OneShot = p2.OneShot,
                Emitting = p2.Emitting,
                Explosiveness = p2.Explosiveness,
                Randomness = p2.Randomness,
                SpeedScale = p2.SpeedScale,
                Preprocess = p2.Preprocess,
                LocalCoords = p2.LocalCoords
            };
        }

        /// <summary>
        /// Apply each PROVIDED (non-null) value to the live particle node, clamping it through the pure-managed
        /// <see cref="ParticlesDefaults"/> rules first so an out-of-range value can never reach the node. Null
        /// arguments leave the corresponding property untouched.
        /// </summary>
        static void ApplyConfig(
            Node node,
            int? amount, double? lifetime, bool? oneShot,
            float? explosiveness, float? randomness,
            double? speedScale, double? preprocess, bool? localCoords)
        {
            if (node is GpuParticles3D p3)
            {
                if (amount.HasValue) p3.Amount = ParticlesDefaults.ClampAmount(amount.Value);
                if (lifetime.HasValue) p3.Lifetime = ParticlesDefaults.ClampLifetime(lifetime.Value);
                if (oneShot.HasValue) p3.OneShot = oneShot.Value;
                if (explosiveness.HasValue) p3.Explosiveness = ParticlesDefaults.ClampRatio(explosiveness.Value);
                if (randomness.HasValue) p3.Randomness = ParticlesDefaults.ClampRatio(randomness.Value);
                if (speedScale.HasValue) p3.SpeedScale = ParticlesDefaults.ClampSpeedScale(speedScale.Value);
                if (preprocess.HasValue) p3.Preprocess = ParticlesDefaults.ClampPreprocess(preprocess.Value);
                if (localCoords.HasValue) p3.LocalCoords = localCoords.Value;
                return;
            }

            var p2 = (GpuParticles2D)node;
            if (amount.HasValue) p2.Amount = ParticlesDefaults.ClampAmount(amount.Value);
            if (lifetime.HasValue) p2.Lifetime = ParticlesDefaults.ClampLifetime(lifetime.Value);
            if (oneShot.HasValue) p2.OneShot = oneShot.Value;
            if (explosiveness.HasValue) p2.Explosiveness = ParticlesDefaults.ClampRatio(explosiveness.Value);
            if (randomness.HasValue) p2.Randomness = ParticlesDefaults.ClampRatio(randomness.Value);
            if (speedScale.HasValue) p2.SpeedScale = ParticlesDefaults.ClampSpeedScale(speedScale.Value);
            if (preprocess.HasValue) p2.Preprocess = ParticlesDefaults.ClampPreprocess(preprocess.Value);
            if (localCoords.HasValue) p2.LocalCoords = localCoords.Value;
        }

        /// <summary>Set the node's emitting state, optionally restarting the emission from a clean state.</summary>
        static void SetEmittingState(Node node, bool emitting, bool restart)
        {
            if (node is GpuParticles3D p3)
            {
                if (restart) p3.Restart();
                p3.Emitting = emitting;
                return;
            }

            var p2 = (GpuParticles2D)node;
            if (restart) p2.Restart();
            p2.Emitting = emitting;
        }
    }
}
#endif
