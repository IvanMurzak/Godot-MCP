/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Reference to a Godot <see cref="global::Godot.Node"/> in the scene tree. The Godot analog of
    /// Unity-MCP's <c>GameObjectRef</c>. A node can be located either by its scene-tree path (e.g.
    /// <c>"/root/Main/Player"</c>) or by its instance id (<see cref="global::Godot.GodotObject.GetInstanceId"/>).
    ///
    /// Plain data model — holds no live <see cref="global::Godot.Node"/> handle and touches no Godot
    /// API, so it serializes/deserializes via ReflectorNet off the main thread. Resolution to a live
    /// node (which DOES touch the scene tree) is the caller's responsibility, on the main thread.
    /// </summary>
    [System.Serializable]
    [Description("Reference to a Godot Node in the scene tree, located by scene-tree path or instance id.")]
    public class NodeRef
    {
        public static class NodeRefProperty
        {
            public const string InstanceId = "instanceId";
            public const string Path = "path";

            public static IEnumerable<string> All => new[] { InstanceId, Path };
        }

        [JsonInclude, JsonPropertyName(NodeRefProperty.InstanceId)]
        [Description("Instance id of the Node (Godot GodotObject.GetInstanceId()). If '0', treated as unset. Priority: 1.")]
        public ulong InstanceId { get; set; } = 0;

        [JsonInclude, JsonPropertyName(NodeRefProperty.Path)]
        [Description("Scene-tree path of the Node, e.g. '/root/Main/Player' or 'Main/Player'. Priority: 2.")]
        public string? Path { get; set; } = null;

        public NodeRef() { }

        public NodeRef(ulong instanceId)
        {
            InstanceId = instanceId;
        }

        public NodeRef(string? path)
        {
            Path = path;
        }

        public virtual bool IsValid() => IsValid(out _);

        public virtual bool IsValid(out string? error)
        {
            if (InstanceId != 0)
            {
                error = null;
                return true;
            }
            if (!string.IsNullOrEmpty(Path))
            {
                error = null;
                return true;
            }

            error = $"At least one of '{NodeRefProperty.InstanceId}' (non-zero) or '{NodeRefProperty.Path}' (non-empty) must be set.";
            return false;
        }

        public override string ToString()
        {
            if (InstanceId != 0)
                return $"Node {NodeRefProperty.InstanceId}='{InstanceId}'";
            if (!string.IsNullOrEmpty(Path))
                return $"Node {NodeRefProperty.Path}='{Path}'";
            return "Node unknown";
        }
    }
}
