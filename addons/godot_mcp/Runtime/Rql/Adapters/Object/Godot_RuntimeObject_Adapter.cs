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

        /// <summary>
        /// A Godot class (Node3D, StandardMaterial3D …): <c>id</c> is its identity, so in a result it reads as its reference
        /// (<c>{$ref: Node3D#12, name: …}</c>, the form <c>ref("Node3D#12")</c> takes back) instead of every C# member
        /// (Multiplayer peers, StringName internals …). A project's own class (a script node, a data Resource) keeps its own
        /// members, written out in a result (a game has no query tool to read them otherwise); only the engine's are hidden.
        /// </summary>
        protected override void Members(MemberTable<GodotObject> m)
        {
            m.Add<ulong>("id", o => o.GetInstanceId(), description: "Godot instance id (the number in $ref)")
             .Add<string>("class", o => o.GetClass(), description: "Godot class name, e.g. \"OmniLight3D\"")
             .Add<string?>("name", o => NameOf(o), description: "a node's name, a resource's path (or its name)");
            // The table's type is the one its virtual members are declared on (MemberTable does not expose it).
            Type? type = null;
            m.HideWhere(member => { if (member.Name == "id" && member.Member == null) type = member.DeclaringType; return false; });
            var godot = typeof(GodotObject).Assembly;
            if (type != null && type.Assembly != godot)
            {
                m.HideWhere(member => member.Member?.DeclaringType?.Assembly == godot);
                return;
            }
            // The C# Name is a StringName; "name" reads it as text (and the two names would clash case-insensitively).
            m.HideWhere(member => member.Name == "Name" && member.Type == typeof(StringName));
            m.Identity("id");
            m.Display("name");
        }

        internal static string? NameOf(GodotObject o) => o switch
        {
            Node n => n.Name.ToString(),
            Resource r => string.IsNullOrEmpty(r.ResourcePath) ? (string.IsNullOrEmpty(r.ResourceName) ? null : r.ResourceName) : r.ResourcePath,
            _ => null,
        };

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
