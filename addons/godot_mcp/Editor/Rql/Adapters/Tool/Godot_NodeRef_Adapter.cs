/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Globalization;
using com.IvanMurzak.Godot.MCP.Data;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// NodeRef tool arguments (script-attach-to-node, screenshot-camera, screenshot-isolated) in RQL terms: a node from
    /// <c>ref("Node3D#12")</c> / <c>godot.find("Name")</c>, an instance id number, or text — a path below the edited scene
    /// root (or a node name). Pure-managed for the node-free forms, so it is unit-tested.
    /// </summary>
    public sealed class Godot_NodeRef_Adapter : RqlTypeAdapter
    {
        public Godot_NodeRef_Adapter() : base(typeof(NodeRef)) { }

        public override string? Description =>
            "a node: ref(\"Node3D#12\") from a query, godot.find(\"Name\"), an instance id, or its path below the scene root as text (\"Level/Player\")";

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            reason = null;
            result = value switch
            {
                NodeRef existing => existing,
                global::Godot.GodotObject o when global::Godot.GodotObject.IsInstanceValid(o) => new NodeRef(o.GetInstanceId()),
                string s when s.Length > 0 => new NodeRef(s),
                double d when d >= 1 && Math.Floor(d) == d => new NodeRef((ulong)d),
                long l when l >= 1 => new NodeRef((ulong)l),
                _ => null,
            };
            if (result == null && value is double bad)
            {
                reason = $"{bad.ToString(CultureInfo.InvariantCulture)} is not an instance id";
                return AdapterOutcome.Failed;
            }
            return result != null ? AdapterOutcome.Ok : AdapterOutcome.NotHandled;
        }
    }
}
#endif
