/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Node
    {
        public const string NodeCreateToolId = "node-create";

        [AiTool
        (
            NodeCreateToolId,
            Title = "Node / Create"
        )]
        [Description("Create a new Node in the currently edited Godot scene and return its structured data. " +
            "Three creation modes (choose exactly one):\n" +
            "  1. Empty/typed — pass 'typeClassName' (a Godot class like 'Node3D', 'Sprite2D', 'Node'). " +
            "Defaults to 'Node' when omitted.\n" +
            "  2. Instanced scene — pass 'instanceScenePath' (a res:// path to a .tscn/.scn PackedScene); " +
            "the scene is instanced and added as a child.\n" +
            "When both are supplied, 'instanceScenePath' wins. " +
            "Optionally pass 'parentNodeRef' to parent the new Node (defaults to the edited scene root) and " +
            "'name' to rename it. The new Node's owner is set to the edited scene root so it is saved with the scene.")]
        public NodeData Create
        (
            [Description("Name for the new Node. When omitted, Godot's default name for the type is used.")]
            string? name = null,
            [Description("Godot class name to instantiate (e.g. 'Node3D', 'Sprite2D', 'Node'). Used when " +
                "'instanceScenePath' is not provided. Defaults to 'Node'.")]
            string? typeClassName = null,
            [Description("res:// path to a PackedScene (.tscn/.scn) to instance as the new Node. Takes " +
                "precedence over 'typeClassName' when both are supplied.")]
            string? instanceScenePath = null,
            [Description("Reference to the parent Node. When omitted, the Node is parented to the edited scene root.")]
            NodeRef? parentNodeRef = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var root = EditorInterface.Singleton.GetEditedSceneRoot()
                    ?? throw new Exception("No scene is currently being edited; open or create a scene first.");

                // Resolve the parent (default: edited scene root). An explicit parentNodeRef must resolve
                // (and throws when it does not) — only a genuinely-null ref falls back to the scene root.
                Node parent = root;
                if (parentNodeRef != null)
                {
                    parent = ResolveNode(parentNodeRef, out var parentError)
                        ?? throw new ArgumentException(parentError ?? "Parent node not found.", nameof(parentNodeRef));
                }

                // Build the node: instanced scene takes precedence over typed instantiation.
                Node node;
                if (!string.IsNullOrEmpty(instanceScenePath))
                {
                    if (!ResourceLoader.Exists(instanceScenePath))
                        throw new ArgumentException($"No resource exists at '{instanceScenePath}'.", nameof(instanceScenePath));

                    var packed = ResourceLoader.Load<PackedScene>(instanceScenePath)
                        ?? throw new ArgumentException($"Resource at '{instanceScenePath}' is not a PackedScene.", nameof(instanceScenePath));

                    node = packed.Instantiate()
                        ?? throw new Exception($"Failed to instantiate PackedScene '{instanceScenePath}'.");
                }
                else
                {
                    var className = string.IsNullOrEmpty(typeClassName) ? "Node" : typeClassName!;
                    if (!ClassDB.ClassExists(className))
                        throw new ArgumentException($"Unknown Godot class '{className}'.", nameof(typeClassName));
                    if (!ClassDB.CanInstantiate(className))
                        throw new ArgumentException($"Godot class '{className}' cannot be instantiated (abstract/virtual).", nameof(typeClassName));

                    var instance = ClassDB.Instantiate(className).As<Node>()
                        ?? throw new ArgumentException($"Godot class '{className}' did not instantiate to a Node.", nameof(typeClassName));
                    node = instance;
                }

                if (!string.IsNullOrEmpty(name))
                    node.Name = name;

                parent.AddChild(node);

                // Owner must be the edited scene root for the node (and its sub-tree) to be persisted on save.
                node.Owner = root;
                SetOwnerRecursive(node, root);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(node);

                return ToNodeData(node);
            });
        }

        /// <summary>
        /// Set the scene-root owner on a node's descendants so an instanced sub-tree is saved inline with
        /// the scene. Skips descendants that already have an owner (e.g. nodes internal to an instanced
        /// PackedScene that should remain owned by their own scene). Main-thread only.
        /// </summary>
        static void SetOwnerRecursive(Node node, Node owner)
        {
            var count = node.GetChildCount(includeInternal: false);
            for (int i = 0; i < count; i++)
            {
                var child = node.GetChild(i, includeInternal: false);
                if (child == null)
                    continue;
                if (child.Owner == null)
                {
                    child.Owner = owner;
                    SetOwnerRecursive(child, owner);
                }
            }
        }
    }
}
#endif
