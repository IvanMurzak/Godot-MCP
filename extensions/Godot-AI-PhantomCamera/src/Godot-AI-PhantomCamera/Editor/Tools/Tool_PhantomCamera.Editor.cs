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

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// Editor-only shared helpers for the <c>phantomcamera-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>s). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor main thread.
    ///
    /// <para>
    /// <b>Class B.</b> Phantom Camera nodes are driven dynamically via <see cref="AddonInterop"/> +
    /// <c>GodotObject.Set/Get</c> with <see cref="PhantomCameraNames"/> snake_case members — no compile-time
    /// Phantom Camera type appears anywhere. The built-in <c>Camera3D</c> used by <c>host-create</c> IS a
    /// GodotSharp type (not the addon), so it is referenced directly.
    /// </para>
    /// </summary>
    public partial class Tool_PhantomCamera
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

        /// <summary>True when the addon is installed (its <c>PhantomCamera3D</c> class_name is registered).</summary>
        static bool AddonInstalled() => AddonInterop.GlobalClassExists(PhantomCameraNames.PhantomCamera3D);

        /// <summary>
        /// Structural check that a node is a Phantom Camera — it exposes the addon's <c>follow_mode</c> +
        /// <c>priority</c> properties. (Avoids depending on a compile-time addon type.) Probing a missing
        /// property returns a <c>Nil</c> Variant, never an error.
        /// </summary>
        static bool IsPhantomCamera(Node node) =>
            node.Get(PhantomCameraNames.FollowMode).VariantType != Variant.Type.Nil
            && node.Get(PhantomCameraNames.Priority).VariantType != Variant.Type.Nil;

        /// <summary>
        /// Resolve <paramref name="nodePath"/> (relative to the edited scene root) to a Phantom Camera node,
        /// throwing a clear error when the path is empty, the node is missing, or the node is not a
        /// PhantomCamera.
        /// </summary>
        static Node ResolvePhantomCameraOrThrow(string? nodePath, Node root)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("A node path is required.", nameof(nodePath));

            var node = root.GetNodeOrNull(new NodePath(nodePath))
                ?? throw new ArgumentException($"No node found at path '{nodePath}'.", nameof(nodePath));

            if (!IsPhantomCamera(node))
                throw new ArgumentException(
                    $"Node at '{nodePath}' is not a PhantomCamera3D/PhantomCamera2D.", nameof(nodePath));

            return node;
        }

        /// <summary>Depth-first search for the first <c>Camera3D</c> under <paramref name="node"/> (inclusive).</summary>
        static Camera3D? FindFirstCamera3D(Node node)
        {
            if (node is Camera3D cam) return cam;
            foreach (var child in node.GetChildren())
            {
                var found = FindFirstCamera3D(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>The existing <c>PhantomCameraHost</c> child of <paramref name="camera"/>, or null.</summary>
        static Node? FindHostChild(Node camera)
        {
            foreach (var child in camera.GetChildren())
                if (AddonInterop.ResolveScriptClassName(child) == PhantomCameraNames.PhantomCameraHost)
                    return child;
            return null;
        }

        /// <summary>
        /// Build a pure-managed <see cref="PhantomCameraInfo"/> snapshot from a live Phantom Camera node,
        /// reading the addon's scalar config dynamically (<c>snake_case</c> via <see cref="PhantomCameraNames"/>).
        /// </summary>
        static PhantomCameraInfo ReadInfo(Node pcam)
        {
            var followTarget = pcam.Get(PhantomCameraNames.FollowTarget).As<Node>();
            var lookAtTarget = pcam.Get(PhantomCameraNames.LookAtTarget).As<Node>();

            return new PhantomCameraInfo
            {
                Installed = true,
                Addon = AddonGate.PhantomCameraAddon,
                NodePath = pcam.GetPath().ToString(),
                TypeName = AddonInterop.ResolveScriptClassName(pcam),
                Priority = pcam.Get(PhantomCameraNames.Priority).AsInt32(),
                FollowMode = ((PhantomFollowMode)pcam.Get(PhantomCameraNames.FollowMode).AsInt32()).ToLabel(),
                FollowTarget = followTarget != null ? followTarget.GetPath().ToString() : string.Empty,
                LookAtMode = ((PhantomLookAtMode)pcam.Get(PhantomCameraNames.LookAtMode).AsInt32()).ToLabel(),
                LookAtTarget = lookAtTarget != null ? lookAtTarget.GetPath().ToString() : string.Empty,
                FollowDamping = pcam.Get(PhantomCameraNames.FollowDamping).AsBool(),
                LookAtDamping = pcam.Get(PhantomCameraNames.LookAtDamping).AsBool()
            };
        }
    }
}
#endif
