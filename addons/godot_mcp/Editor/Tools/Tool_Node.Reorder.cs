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
        public const string NodeReorderToolId = "node-reorder";

        [AiTool
        (
            NodeReorderToolId,
            Title = "Node / Reorder",
            IdempotentHint = true
        )]
        [Description("Move a Node to a different position among its parent's children in the currently " +
            "edited Godot scene (Godot's Node.MoveChild). Child order is layout order for containers " +
            "(VBoxContainer/HBoxContainer/GridContainer) and draw order for CanvasItems, and until now it " +
            "could only be set by creating nodes in the right sequence — rearranging an existing scene had " +
            "no path short of delete-and-recreate.\n" +
            "'index' is the 0-based destination among the parent's children; negative counts from the end " +
            "(-1 = last). Out-of-range values clamp into range. Returns the moved Node's updated structured " +
            "data, whose 'index' is the position it actually ended up at — check it rather than assuming.")]
        public NodeData Reorder
        (
            [Description("Reference to the Node to move (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef,
            [Description("0-based destination index among the parent's children. Negative counts from the " +
                "end (-1 = last). Out-of-range values clamp into range.")]
            int index
        )
        {
            return MainThread.Instance.Run(() =>
            {
                EditorToolGuards.GetEditedSceneRootOrThrow();

                var node = ResolveNode(nodeRef, out var error)
                    ?? throw new ArgumentException(error ?? "Node not found.", nameof(nodeRef));

                var parent = node.GetParent()
                    ?? throw new ArgumentException(
                        "Cannot reorder a Node with no parent (the scene root has no siblings). Use " +
                        $"'{NodeSetParentToolId}' to give it a parent first.", nameof(nodeRef));

                // Clamp against the parent's NON-internal child count: internal children are excluded from
                // everything else in this family, and clamping to the smaller count can never hand MoveChild
                // an out-of-range index (which Godot answers with an error + a silent no-op).
                var target = NodeIndexResolver.Resolve(index, parent.GetChildCount(includeInternal: false));
                parent.MoveChild(node, target);

                EditorInterface.Singleton.MarkSceneAsUnsaved();

                return ToNodeData(node);
            });
        }
    }
}
#endif
