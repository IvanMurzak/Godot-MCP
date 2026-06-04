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

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Node
    {
        public const string NodeFindToolId = "node-find";

        [AiTool
        (
            NodeFindToolId,
            Title = "Node / Find",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Find a Node in the currently edited Godot scene by instance id or scene-tree path " +
            "and return its structured data (instanceId, name, path, type, attached script, child count). " +
            "Provide a 'nodeRef' identifying the Node (instanceId is preferred; a path like " +
            "'/root/Main/Player' or 'Main/Player' is resolved relative to the edited scene root). " +
            "Set 'hierarchyDepth' > 0 to include children: 1 = direct children, 2 = grandchildren, etc. " +
            "A node that cannot be resolved (bad ref, no edited scene, path not found) yields a structured " +
            "error, not a null result.")]
        public NodeData? Find
        (
            [Description("Reference to the Node to find (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef,
            [Description("Depth of children to include. 0 = the target Node only; 1 = one layer of children; etc.")]
            int hierarchyDepth = 0
        )
        {
            if (hierarchyDepth < 0)
                throw new ArgumentException("hierarchyDepth must be >= 0.", nameof(hierarchyDepth));

            return MainThread.Instance.Run(() =>
            {
                // ResolveNode sets 'error' on every null return (bad ref, no edited scene, path not found),
                // so a null node always comes with an error here — surfaced as a structured error rather
                // than a soft-null result (see the tool description). There is no error-free null case.
                var node = ResolveNode(nodeRef, out var error);
                if (node == null)
                    throw new Exception(error ?? $"Node by {nodeRef} not found.");

                return ToNodeData(node, hierarchyDepth);
            });
        }
    }
}
#endif
