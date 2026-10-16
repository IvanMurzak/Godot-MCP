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

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// Editor-only shared helpers for the <c>terrain3d-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>s, and drive the Terrain3D GDExtension dynamically by string
    /// name — there are no compile-time Terrain3D types). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor main thread.
    ///
    /// <para>
    /// The Terrain3D node is driven via <c>GodotObject.Set/Get/Call</c> using the GDScript <b>snake_case</b>
    /// member names centralized in <see cref="Terrain3DNames"/> (the CLASS-B contract). Terrain3D's
    /// <c>region_size</c> getter/setter int values ARE the region edge length (e.g. <c>SIZE_1024</c> = 1024).
    /// </para>
    /// </summary>
    public partial class Tool_Terrain3D
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
        /// Resolve <paramref name="nodePath"/> (relative to the edited scene root) to a Terrain3D node, throwing
        /// a clear error when the path is empty, the node is missing, or the node is not a <c>Terrain3D</c>
        /// (checked dynamically via <c>Node.IsClass</c> — there is no compile-time Terrain3D type).
        /// </summary>
        static Node ResolveTerrainNodeOrThrow(string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("A node path is required.", nameof(nodePath));

            var root = GetEditedSceneRootOrThrow();
            var node = root.GetNodeOrNull(new NodePath(nodePath));
            if (node == null)
                throw new ArgumentException($"No node found at path '{nodePath}'.", nameof(nodePath));

            if (!node.IsClass(Terrain3DNames.TerrainClass))
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not a {Terrain3DNames.TerrainClass}.",
                    nameof(nodePath));

            return node;
        }

        /// <summary>
        /// Build a pure-managed <see cref="Terrain3DNodeInfo"/> snapshot from a live Terrain3D node, reading its
        /// scalar config dynamically by snake_case name. The gate has already passed, so
        /// <see cref="Terrain3DNodeInfo.Installed"/> is true.
        /// </summary>
        static Terrain3DNodeInfo ReadInfo(Node node)
        {
            var info = Terrain3DNodeInfo.FromGate(AddonGate.Ok());
            info.NodePath = node.GetPath().ToString();
            info.TypeName = node.GetClass();
            info.DataDirectory = node.Get(Terrain3DNames.DataDirectoryProperty).AsString();
            info.RegionSize = node.Get(Terrain3DNames.RegionSizeProperty).AsInt32();
            info.Version = node.Get(Terrain3DNames.VersionProperty).AsString();
            info.MeshLods = node.Get(Terrain3DNames.MeshLodsProperty).AsInt32();
            info.MeshSize = node.Get(Terrain3DNames.MeshSizeProperty).AsInt32();
            info.VertexSpacing = node.Get(Terrain3DNames.VertexSpacingProperty).AsDouble();
            info.HasMaterial = node.Get(Terrain3DNames.MaterialProperty).As<GodotObject>() != null;

            var data = node.Get(Terrain3DNames.DataProperty).As<GodotObject>();
            info.RegionCount = data != null ? data.Call(Terrain3DNames.GetRegionCountMethod).AsInt32() : 0;

            return info;
        }
    }
}
#endif
