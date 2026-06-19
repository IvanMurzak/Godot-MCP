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
        public const string NodeSetParentToolId = "node-set-parent";

        [AiTool
        (
            NodeSetParentToolId,
            Title = "Node / Set Parent",
            IdempotentHint = true
        )]
        [Description("Reparent a Node under a new parent in the currently edited Godot scene, preserving " +
            "its global transform by default (Godot's Node.Reparent). Identify the moving Node with " +
            "'nodeRef' and the destination with 'newParentNodeRef'. Set 'keepGlobalTransform' to false to " +
            "keep the Node's local transform instead. Returns the reparented Node's updated structured data.")]
        public NodeData SetParent
        (
            [Description("Reference to the Node to reparent (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef,
            [Description("Reference to the new parent Node.")]
            NodeRef newParentNodeRef,
            [Description("When true (default), preserve the Node's global transform across the reparent. " +
                "When false, keep its local transform.")]
            bool keepGlobalTransform = true
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var root = EditorInterface.Singleton.GetEditedSceneRoot()
                    ?? throw new Exception("No scene is currently being edited.");

                var node = ResolveNode(nodeRef, out var error)
                    ?? throw new ArgumentException(error ?? "Node not found.", nameof(nodeRef));

                var newParent = ResolveNode(newParentNodeRef, out var parentError)
                    ?? throw new ArgumentException(parentError ?? "New parent Node not found.", nameof(newParentNodeRef));

                if (node == root)
                    throw new ArgumentException("Cannot reparent the scene root Node.", nameof(nodeRef));
                if (node == newParent)
                    throw new ArgumentException("Cannot reparent a Node under itself.", nameof(newParentNodeRef));
                if (IsAncestorOf(node, newParent))
                    throw new ArgumentException("Cannot reparent a Node under one of its own descendants.", nameof(newParentNodeRef));

                node.Reparent(newParent, keepGlobalTransform: keepGlobalTransform);

                // Preserve scene persistence: the reparented sub-tree must still be owned by the scene root.
                if (node.Owner == null)
                    node.Owner = root;
                SetOwnerRecursive(node, root);

                EditorInterface.Singleton.MarkSceneAsUnsaved();

                return ToNodeData(node);
            });
        }

        /// <summary>True when <paramref name="ancestor"/> is an ancestor of <paramref name="candidate"/>.</summary>
        static bool IsAncestorOf(Node ancestor, Node candidate)
        {
            var cursor = candidate.GetParent();
            while (cursor != null)
            {
                if (cursor == ancestor)
                    return true;
                cursor = cursor.GetParent();
            }
            return false;
        }
    }
}
#endif
