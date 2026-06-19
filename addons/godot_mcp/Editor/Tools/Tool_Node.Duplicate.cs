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
        public const string NodeDuplicateToolId = "node-duplicate";

        [AiTool
        (
            NodeDuplicateToolId,
            Title = "Node / Duplicate"
        )]
        [Description("Duplicate a Node (and its whole sub-tree) in the currently edited Godot scene via " +
            "Godot's Node.Duplicate, adding the copy as a sibling under the same parent. Identify the source " +
            "with 'nodeRef'. Optionally pass 'name' to rename the duplicate. Returns the new Node's structured data.")]
        public NodeData Duplicate
        (
            [Description("Reference to the Node to duplicate (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef,
            [Description("Optional name for the duplicate. When omitted, Godot assigns a unique sibling name.")]
            string? name = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var root = EditorInterface.Singleton.GetEditedSceneRoot()
                    ?? throw new Exception("No scene is currently being edited.");

                var node = ResolveNode(nodeRef, out var error)
                    ?? throw new ArgumentException(error ?? "Node not found.", nameof(nodeRef));

                if (node == root)
                    throw new ArgumentException("Cannot duplicate the scene root Node.", nameof(nodeRef));

                var parent = node.GetParent()
                    ?? throw new Exception($"Node '{node.Name}' has no parent; cannot place a duplicate.");

                var duplicate = node.Duplicate()
                    ?? throw new Exception($"Failed to duplicate Node '{node.Name}'.");

                if (!string.IsNullOrEmpty(name))
                    duplicate.Name = name;

                parent.AddChild(duplicate);

                // Own the duplicate (and its sub-tree) by the scene root so it persists on save.
                duplicate.Owner = root;
                SetOwnerRecursive(duplicate, root);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                EditorInterface.Singleton.EditNode(duplicate);

                return ToNodeData(duplicate);
            });
        }
    }
}
#endif
