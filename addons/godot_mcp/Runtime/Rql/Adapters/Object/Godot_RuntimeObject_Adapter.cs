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
using System.Globalization;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Every Godot object inside a running game (the in-game engine only, <see cref="GodotRqlRuntime"/>; the editor has
    /// <c>Godot_GodotObject_Adapter</c>): identity (<c>$ref</c> "Class#instance id"), liveness (a freed object reads as
    /// null), <c>id</c> and <c>class</c>, and — where a node or resource is expected — a node path or name in the running
    /// scene, or a <c>res://</c> / <c>uid://</c> path.
    /// </summary>
    public sealed class Godot_RuntimeObject_Adapter : RqlTypeAdapter<GodotObject>
    {
        public override string Description =>
            "Godot object: id, class; give one as ref(\"Node3D#12\"), godot.find(\"Name\"), a node path \"Level/Player\" or a resource path \"res://…\"";

        protected override string? Id(GodotObject o) =>
            GodotObject.IsInstanceValid(o) ? o.GetInstanceId().ToString(CultureInfo.InvariantCulture) : null;

        protected override bool? Alive(GodotObject o) => GodotObject.IsInstanceValid(o);

        protected override void Members(MemberTable<GodotObject> m) =>
            m.Add<ulong>("id", o => o.GetInstanceId(), description: "Godot instance id (the number in $ref)")
             .Add<string>("class", o => o.GetClass(), description: "Godot class name, e.g. \"OmniLight3D\"");

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (!(value is string text) || text.Length == 0) return AdapterOutcome.NotHandled;
            var wantsResource = typeof(Resource).IsAssignableFrom(target) || target.IsAssignableFrom(typeof(Resource));
            var wantsNode = typeof(Node).IsAssignableFrom(target) || target.IsAssignableFrom(typeof(Node));
            GodotObject? found;
            if (wantsResource && (text.StartsWith("res://", StringComparison.Ordinal) || text.StartsWith("uid://", StringComparison.Ordinal)))
            {
                if (!GodotRqlRuntime.TryLoadResource(text, out var resource, out reason)) return AdapterOutcome.Failed;
                found = resource;
            }
            else if (wantsNode)
            {
                if (!GodotRqlRuntime.TryFindNode(text, out var node, out reason)) return AdapterOutcome.Failed;
                found = node;
            }
            else return AdapterOutcome.NotHandled;
            if (!target.IsInstanceOfType(found)) { reason = $"'{text}' is a {found!.GetClass()}, expected {target.Name}"; return AdapterOutcome.Failed; }
            result = found;
            return AdapterOutcome.Ok;
        }
    }
}
