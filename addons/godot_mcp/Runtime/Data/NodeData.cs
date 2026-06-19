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
    /// Structured, engine-agnostic snapshot of a Godot <see cref="global::Godot.Node"/> returned by the
    /// node tool family (the Godot analog of Unity-MCP's <c>GameObjectData</c>). Holds no live
    /// <see cref="global::Godot.Node"/> handle — it is built on the main thread from a resolved node and
    /// then serialized off the main thread, so it touches no Godot native object once constructed.
    ///
    /// <para>
    /// Godot has no Unity-style <c>Component</c> concept: a node IS its type plus its built-in properties
    /// plus an optional attached script. So <see cref="Type"/> carries the node's class name (e.g.
    /// <c>"Node3D"</c>) and <see cref="ScriptResourcePath"/> carries the <c>res://</c> path of the
    /// attached script if any — together they model what a Unity GameObject would express as a list of
    /// components.
    /// </para>
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain
    /// xUnit host.
    /// </summary>
    [System.Serializable]
    [Description("Structured snapshot of a Godot Node: identity (instanceId/name/path), type, optional " +
        "attached-script path, child count, and optional children.")]
    public class NodeData
    {
        [JsonInclude, JsonPropertyName("instanceId")]
        [Description("Instance id of the Node (Godot GodotObject.GetInstanceId()). Stable identity within the session.")]
        public ulong InstanceId { get; set; } = 0;

        [JsonInclude, JsonPropertyName("name")]
        [Description("Node name (the last segment of its scene-tree path).")]
        public string Name { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("path")]
        [Description("Absolute scene-tree path of the Node, e.g. '/root/Main/Player'.")]
        public string Path { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("type")]
        [Description("Godot class name of the Node, e.g. 'Node3D', 'Sprite2D'. The Godot analog of a Unity component set.")]
        public string Type { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("scriptResourcePath")]
        [Description("res:// path of the script attached to the Node, or null when no script is attached.")]
        public string? ScriptResourcePath { get; set; } = null;

        [JsonInclude, JsonPropertyName("childCount")]
        [Description("Number of direct children of the Node (excluding internal children).")]
        public int ChildCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("children")]
        [Description("Direct/recursive children, populated only when a hierarchy depth > 0 was requested. " +
            "Null when no hierarchy was requested.")]
        public List<NodeData>? Children { get; set; } = null;

        public NodeData() { }

        public override string ToString()
            => $"Node '{Name}' ({Type}) instanceId={InstanceId} path='{Path}'";
    }
}
