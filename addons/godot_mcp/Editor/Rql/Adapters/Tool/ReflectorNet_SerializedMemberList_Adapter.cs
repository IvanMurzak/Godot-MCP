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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.ReflectorNet.Model;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// SerializedMemberList tool arguments (script-execute parameters) in RQL terms, as in Unity-MCP: named values
    /// <c>{count: 3, target: ref("Node3D#12")}</c>, each becoming a parameter of its value's type. The ReflectorNet form
    /// (a list of {name, typeName, value}) still works. Pure-managed except for a node value's instance id.
    /// </summary>
    public sealed class ReflectorNet_SerializedMemberList_Adapter : RqlTypeAdapter
    {
        public ReflectorNet_SerializedMemberList_Adapter() : base(typeof(SerializedMemberList)) { }

        public override string? Description =>
            "named values {count: 3, label: \"x\", target: ref(\"Node3D#12\")}: each is passed as a parameter of the value's type (a node, string, int, float, bool)";

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (value is ObjectValue named)
            {
                var list = new SerializedMemberList();
                foreach (var kv in named)
                {
                    if (!TryMember(kv.Key, kv.Value, out var member, out reason)) return AdapterOutcome.Failed;
                    list.Add(member!);
                }
                result = list;
                return AdapterOutcome.Ok;
            }
            if (value is List<object?> items && items.All(i => i is ObjectValue o && o.ContainsKey("typeName")))
            {
                var list = new SerializedMemberList();
                foreach (var o in items.Cast<ObjectValue>())
                {
                    var member = new SerializedMember { name = o.TryGetValue("name", out var n) ? n as string : null, typeName = o["typeName"] as string ?? "" };
                    if (o.TryGetValue("value", out var v)) member.SetJsonValue(ToJson(v)?.ToJsonString());
                    list.Add(member);
                }
                result = list;
                return AdapterOutcome.Ok;
            }
            return AdapterOutcome.NotHandled;
        }

        private static bool TryMember(string name, object? value, out SerializedMember? member, out string? reason)
        {
            reason = null;
            // RQL numbers are doubles: a whole one is passed as an integer (one rule for the type and the JSON).
            if (value is double whole && Math.Floor(whole) == whole && Math.Abs(whole) < 9e15)
                value = (long)whole;
            var typeName = value switch
            {
                null => "System.Object",
                global::Godot.Node node => node.GetType().FullName!,
                string _ => "System.String",
                bool _ => "System.Boolean",
                long l when l >= int.MinValue && l <= int.MaxValue => "System.Int32",
                long _ => "System.Int64",
                double _ => "System.Single",
                _ => null,
            };
            member = null;
            if (typeName == null)
            {
                reason = $"parameter '{name}': {value!.GetType().Name} values are not supported (use a node reference, text, a number or a bool)";
                return false;
            }
            member = new SerializedMember { name = name, typeName = typeName };
            member.SetJsonValue(ToJson(value)?.ToJsonString());
            return true;
        }

        // JSON in the form script-execute's ReflectorNet deserialization expects; nodes as {"instanceId": N}.
        private static JsonNode? ToJson(object? v) => v switch
        {
            null => null,
            bool b => JsonValue.Create(b),
            string s => JsonValue.Create(s),
            long l => JsonValue.Create(l),
            double d when Math.Floor(d) == d && Math.Abs(d) < 9e15 => JsonValue.Create((long)d),
            double d => JsonValue.Create(d),
            global::Godot.Node node => new JsonObject { [NodeRef.NodeRefProperty.InstanceId] = node.GetInstanceId() },
            ObjectValue o => new JsonObject(o.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, ToJson(kv.Value)))),
            List<object?> l => new JsonArray(l.Select(ToJson).ToArray()),
            _ => JsonValue.Create(Convert.ToString(v, CultureInfo.InvariantCulture)),
        };
    }
}
