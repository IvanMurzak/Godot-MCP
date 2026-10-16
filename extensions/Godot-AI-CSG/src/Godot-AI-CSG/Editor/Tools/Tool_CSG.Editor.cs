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

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>
    /// Editor-only shared helpers for the <c>csg-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>s). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor main thread.
    ///
    /// <para>
    /// The reads/writes use the strongly-typed <c>CsgBox3D</c> / <c>CsgSphere3D</c> / <c>CsgCylinder3D</c> /
    /// <c>CsgCombiner3D</c> API on purpose — that typed surface (resolved from the consumer's own GodotSharp)
    /// is exactly what the source-only packaging recipe must compile against cross-version. The scalar
    /// property names (<c>Size</c>, <c>Radius</c>, <c>Height</c>, <c>RadialSegments</c>, <c>Operation</c>) are
    /// stable across Godot 4.3 … 4.5, so one info shape (<see cref="CsgShapeInfo"/>) covers all four node types.
    /// </para>
    /// </summary>
    public partial class Tool_CSG
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
        /// Resolve <paramref name="nodePath"/> (relative to the edited scene root) to a CSG node, throwing a
        /// clear error when the path is empty, the node is missing, or the node is not a <c>CsgShape3D</c>.
        /// </summary>
        static CsgShape3D ResolveCsgNodeOrThrow(string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("A node path is required.", nameof(nodePath));

            var root = GetEditedSceneRootOrThrow();
            var node = root.GetNodeOrNull(new NodePath(nodePath));
            if (node == null)
                throw new ArgumentException($"No node found at path '{nodePath}'.", nameof(nodePath));

            if (node is not CsgShape3D shape)
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not a CSG node.", nameof(nodePath));

            return shape;
        }

        /// <summary>
        /// Add a freshly-created CSG <paramref name="node"/> to the edited scene under
        /// <paramref name="parentPath"/> (or the scene root when null/empty), name it, set its owner so it
        /// persists with the scene, select it, and mark the scene unsaved.
        /// </summary>
        static void AttachToScene(Node node, string? name, string? parentPath)
        {
            var root = GetEditedSceneRootOrThrow();

            Node parent = root;
            if (!string.IsNullOrWhiteSpace(parentPath))
                parent = root.GetNodeOrNull(new NodePath(parentPath))
                    ?? throw new ArgumentException($"No parent node found at path '{parentPath}'.", nameof(parentPath));

            if (!string.IsNullOrWhiteSpace(name))
                node.Name = name;

            parent.AddChild(node);
            node.Owner = root; // so the node is persisted when the scene is saved

            EditorInterface.Singleton.MarkSceneAsUnsaved();
            EditorInterface.Singleton.EditNode(node);
        }

        /// <summary>Map the pure-managed <see cref="CsgOperation"/> onto Godot's <c>CsgShape3D.OperationEnum</c>.</summary>
        static CsgShape3D.OperationEnum ToGodotOperation(CsgOperation operation) =>
            (CsgShape3D.OperationEnum)(int)operation;

        /// <summary>Map Godot's <c>CsgShape3D.OperationEnum</c> back to the pure-managed <see cref="CsgOperation"/>.</summary>
        static CsgOperation FromGodotOperation(CsgShape3D.OperationEnum operation) =>
            (CsgOperation)(int)operation;

        /// <summary>Build a pure-managed <see cref="CsgShapeInfo"/> snapshot from a live CSG node.</summary>
        static CsgShapeInfo ReadInfo(CsgShape3D shape)
        {
            var info = new CsgShapeInfo
            {
                NodePath = shape.GetPath().ToString(),
                TypeName = shape.GetClass(),
                Operation = FromGodotOperation(shape.Operation).ToLabel()
            };

            switch (shape)
            {
                case CsgBox3D box:
                    info.Kind = CsgPrimitiveKind.Box.ToLabel();
                    info.SizeX = box.Size.X;
                    info.SizeY = box.Size.Y;
                    info.SizeZ = box.Size.Z;
                    break;
                case CsgSphere3D sphere:
                    info.Kind = CsgPrimitiveKind.Sphere.ToLabel();
                    info.Radius = sphere.Radius;
                    info.RadialSegments = sphere.RadialSegments;
                    break;
                case CsgCylinder3D cylinder:
                    info.Kind = CsgPrimitiveKind.Cylinder.ToLabel();
                    info.Radius = cylinder.Radius;
                    info.Height = cylinder.Height;
                    break;
                case CsgCombiner3D:
                    info.Kind = CsgPrimitiveKind.Combiner.ToLabel();
                    break;
                default:
                    info.Kind = shape.GetClass();
                    break;
            }

            return info;
        }
    }
}
#endif
