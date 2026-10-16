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
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Node
    {
        public const string NodeGetDataToolId = "node-get-data";

        [AiTool
        (
            NodeGetDataToolId,
            Title = "Node / Get Data",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Get the properties of a Node in the currently edited Godot scene — every storable property " +
            "(what the scene file can save for it, current values including defaults; Godot names such as " +
            "'position', 'light_energy', GDScript/C# script properties). Each property's name and value can be sent back to '" + NodeModifyToolId + "' unchanged. " +
            "Identify the Node with 'nodeRef' (instanceId preferred, else scene-tree path); use '" + NodeFindToolId +
            "' to locate it first. Returns a ReflectorNet SerializedMember whose value is the node reference and " +
            "whose props are the properties.")]
        public SerializedMember GetData
        (
            [Description("Reference to the Node to read (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var node = ResolveNode(nodeRef, out var error);
                if (node == null)
                    throw new Exception(error ?? $"Node by {nodeRef} not found.");

                var reflector = GodotMcpReflector.GetOrCreate();
                var member = reflector.Serialize(obj: node, name: node.Name.ToString(), recursive: true);
                GodotStoredProperties.AddTo(member, reflector, node);
                return member;
            });
        }
    }
}
#endif
