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
        public const string NodeDeleteToolId = "node-delete";

        [AiTool
        (
            NodeDeleteToolId,
            Title = "Node / Delete"
        )]
        [Description("Delete a Node (and all of its children) from the currently edited Godot scene. " +
            "Identify the Node with 'nodeRef'. The Node is removed from its parent and freed immediately " +
            "(synchronous free, required for editor-mode edits). Returns the deleted Node's identity " +
            "(instanceId, name, path) for confirmation.")]
        public NodeData Delete
        (
            [Description("Reference to the Node to delete (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var root = EditorInterface.Singleton.GetEditedSceneRoot()
                    ?? throw new Exception("No scene is currently being edited.");

                var node = ResolveNode(nodeRef, out var error)
                    ?? throw new ArgumentException(error ?? "Node not found.", nameof(nodeRef));

                if (node == root)
                    throw new ArgumentException("Cannot delete the scene root Node; close or replace the scene instead.", nameof(nodeRef));

                // Snapshot identity BEFORE freeing — the live handle is invalid afterwards.
                var deleted = ToNodeData(node);

                var parent = node.GetParent();
                parent?.RemoveChild(node);

                // Editor-mode deletes must be immediate (QueueFree defers to the next idle frame, which
                // never ticks deterministically under a headless/tool-driven flow). Node.Free() frees the
                // node and its whole sub-tree synchronously.
                node.Free();

                EditorInterface.Singleton.MarkSceneAsUnsaved();

                return deleted;
            });
        }
    }
}
#endif
