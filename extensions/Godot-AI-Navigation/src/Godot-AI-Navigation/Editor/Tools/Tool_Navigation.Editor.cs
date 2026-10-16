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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>
    /// Editor-only shared helpers for the <c>navigation-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>s). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor main thread.
    ///
    /// <para>
    /// The reads/writes use the strongly-typed <c>NavigationRegion2D/3D</c>, <c>NavigationAgent2D/3D</c> and
    /// <c>NavigationLink2D/3D</c> API on purpose — that typed surface (resolved from the consumer's own
    /// GodotSharp) is exactly what the source-only packaging recipe must compile against cross-version. The
    /// scalar property names are identical on the 2D and 3D node of each family and stable across Godot
    /// 4.3 … 4.5, so one info shape (<see cref="NavigationNodeInfo"/>) covers all of them.
    /// </para>
    /// </summary>
    public partial class Tool_Navigation
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

        /// <summary>Resolve a parent node: the scene root when <paramref name="parentPath"/> is empty, else the node at that path.</summary>
        static Node ResolveParentOrThrow(Node root, string? parentPath)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
                return root;
            return root.GetNodeOrNull(new NodePath(parentPath))
                ?? throw new ArgumentException($"No parent node found at path '{parentPath}'.", nameof(parentPath));
        }

        /// <summary>Add <paramref name="node"/> under <paramref name="parent"/>, owned by <paramref name="root"/> (so it is saved with the scene) and selected in the editor.</summary>
        static void AttachToScene(Node root, Node parent, Node node, string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                node.Name = name;

            parent.AddChild(node);
            node.Owner = root; // so the node is persisted when the scene is saved

            EditorInterface.Singleton.MarkSceneAsUnsaved();
            EditorInterface.Singleton.EditNode(node);
        }

        /// <summary>
        /// Resolve <paramref name="nodePath"/> (relative to the edited scene root) to a node, throwing a clear
        /// error when the path is empty or the node is missing.
        /// </summary>
        static Node ResolveNodeOrThrow(string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("A node path is required.", nameof(nodePath));

            var root = GetEditedSceneRootOrThrow();
            return root.GetNodeOrNull(new NodePath(nodePath))
                ?? throw new ArgumentException($"No node found at path '{nodePath}'.", nameof(nodePath));
        }

        /// <summary>Resolve a node that must be a <c>NavigationRegion2D/3D</c>.</summary>
        static Node ResolveRegionOrThrow(string? nodePath)
        {
            var node = ResolveNodeOrThrow(nodePath);
            if (node is not NavigationRegion2D && node is not NavigationRegion3D)
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not a NavigationRegion2D/3D.", nameof(nodePath));
            return node;
        }

        /// <summary>Resolve a node that must be a <c>NavigationAgent2D/3D</c>.</summary>
        static Node ResolveAgentOrThrow(string? nodePath)
        {
            var node = ResolveNodeOrThrow(nodePath);
            if (node is not NavigationAgent2D && node is not NavigationAgent3D)
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not a NavigationAgent2D/3D.", nameof(nodePath));
            return node;
        }

        /// <summary>Resolve a node that must be one of the six built-in navigation node types.</summary>
        static Node ResolveNavigationNodeOrThrow(string? nodePath)
        {
            var node = ResolveNodeOrThrow(nodePath);
            if (node is not (NavigationRegion2D or NavigationRegion3D
                          or NavigationAgent2D or NavigationAgent3D
                          or NavigationLink2D or NavigationLink3D))
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not a NavigationRegion/Agent/Link (2D or 3D).",
                    nameof(nodePath));
            return node;
        }

        /// <summary>
        /// Apply each PROVIDED (non-null) agent value to the live agent node, clamping it through the
        /// pure-managed <see cref="NavigationDefaults"/> rules first so an out-of-range value can never reach
        /// the node. Null arguments leave the corresponding property untouched.
        /// </summary>
        static void ApplyAgentConfig(
            Node node,
            float? radius, float? pathDesiredDistance, float? targetDesiredDistance,
            float? maxSpeed, bool? avoidanceEnabled)
        {
            if (node is NavigationAgent3D a3)
            {
                if (radius.HasValue) a3.Radius = NavigationDefaults.ClampRadius(radius.Value);
                if (pathDesiredDistance.HasValue) a3.PathDesiredDistance = NavigationDefaults.ClampDistance(pathDesiredDistance.Value);
                if (targetDesiredDistance.HasValue) a3.TargetDesiredDistance = NavigationDefaults.ClampDistance(targetDesiredDistance.Value);
                if (maxSpeed.HasValue) a3.MaxSpeed = NavigationDefaults.ClampMaxSpeed(maxSpeed.Value);
                if (avoidanceEnabled.HasValue) a3.AvoidanceEnabled = avoidanceEnabled.Value;
                return;
            }

            var a2 = (NavigationAgent2D)node;
            if (radius.HasValue) a2.Radius = NavigationDefaults.ClampRadius(radius.Value);
            if (pathDesiredDistance.HasValue) a2.PathDesiredDistance = NavigationDefaults.ClampDistance(pathDesiredDistance.Value);
            if (targetDesiredDistance.HasValue) a2.TargetDesiredDistance = NavigationDefaults.ClampDistance(targetDesiredDistance.Value);
            if (maxSpeed.HasValue) a2.MaxSpeed = NavigationDefaults.ClampMaxSpeed(maxSpeed.Value);
            if (avoidanceEnabled.HasValue) a2.AvoidanceEnabled = avoidanceEnabled.Value;
        }

        /// <summary>Build a pure-managed <see cref="NavigationNodeInfo"/> snapshot from a live navigation node.</summary>
        static NavigationNodeInfo ReadInfo(Node node)
        {
            var info = new NavigationNodeInfo
            {
                NodePath = node.GetPath().ToString(),
                TypeName = node.GetClass()
            };

            switch (node)
            {
                case NavigationRegion3D r3:
                {
                    info.Kind = NavigationKind.Region.ToLabel();
                    info.Dimension = NavigationDimension.Three.ToLabel();
                    info.Enabled = r3.Enabled;
                    var mesh = r3.NavigationMesh;
                    info.ResourcePath = mesh?.ResourcePath ?? string.Empty;
                    info.ResourceType = mesh != null ? mesh.GetClass() : string.Empty;
                    break;
                }
                case NavigationRegion2D r2:
                {
                    info.Kind = NavigationKind.Region.ToLabel();
                    info.Dimension = NavigationDimension.Two.ToLabel();
                    info.Enabled = r2.Enabled;
                    var poly = r2.NavigationPolygon;
                    info.ResourcePath = poly?.ResourcePath ?? string.Empty;
                    info.ResourceType = poly != null ? poly.GetClass() : string.Empty;
                    break;
                }
                case NavigationAgent3D a3:
                {
                    info.Kind = NavigationKind.Agent.ToLabel();
                    info.Dimension = NavigationDimension.Three.ToLabel();
                    info.Radius = a3.Radius;
                    info.PathDesiredDistance = a3.PathDesiredDistance;
                    info.TargetDesiredDistance = a3.TargetDesiredDistance;
                    info.MaxSpeed = a3.MaxSpeed;
                    info.AvoidanceEnabled = a3.AvoidanceEnabled;
                    break;
                }
                case NavigationAgent2D a2:
                {
                    info.Kind = NavigationKind.Agent.ToLabel();
                    info.Dimension = NavigationDimension.Two.ToLabel();
                    info.Radius = a2.Radius;
                    info.PathDesiredDistance = a2.PathDesiredDistance;
                    info.TargetDesiredDistance = a2.TargetDesiredDistance;
                    info.MaxSpeed = a2.MaxSpeed;
                    info.AvoidanceEnabled = a2.AvoidanceEnabled;
                    break;
                }
                case NavigationLink3D l3:
                {
                    info.Kind = NavigationKind.Link.ToLabel();
                    info.Dimension = NavigationDimension.Three.ToLabel();
                    info.Enabled = l3.Enabled;
                    info.StartX = l3.StartPosition.X;
                    info.StartY = l3.StartPosition.Y;
                    info.StartZ = l3.StartPosition.Z;
                    info.EndX = l3.EndPosition.X;
                    info.EndY = l3.EndPosition.Y;
                    info.EndZ = l3.EndPosition.Z;
                    info.Bidirectional = l3.Bidirectional;
                    break;
                }
                case NavigationLink2D l2:
                {
                    info.Kind = NavigationKind.Link.ToLabel();
                    info.Dimension = NavigationDimension.Two.ToLabel();
                    info.Enabled = l2.Enabled;
                    info.StartX = l2.StartPosition.X;
                    info.StartY = l2.StartPosition.Y;
                    info.EndX = l2.EndPosition.X;
                    info.EndY = l2.EndPosition.Y;
                    info.Bidirectional = l2.Bidirectional;
                    break;
                }
            }

            return info;
        }
    }
}
#endif
