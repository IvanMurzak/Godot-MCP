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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Every Godot object: identity (<c>$ref</c> "Class#instance id"), liveness (a freed object reads as null), the Godot
    /// class (<c>class</c>, which also names GDExtension classes the C# type cannot), and <c>props</c> — every Godot
    /// property by its snake_case name, read and written through <see cref="GodotObject.Get"/> / <see cref="GodotObject.Set"/>.
    /// <c>props</c> is what reaches GDScript script variables and GDExtension properties: a CLR <c>is</c> check cannot see a
    /// GDScript class, and those properties have no C# member. Where an object is expected, a <c>res://</c> path loads the
    /// resource and a node path or name finds the node in the edited scene.
    /// </summary>
    public sealed class Godot_GodotObject_Adapter : RqlTypeAdapter<GodotObject>
    {
        public override string Description =>
            "Godot object: id, class, props[\"snake_name\"] (every Godot property, GDScript variables included)";

        protected override string? Id(GodotObject o) =>
            GodotObject.IsInstanceValid(o) ? o.GetInstanceId().ToString(CultureInfo.InvariantCulture) : null;

        protected override bool? Alive(GodotObject o) => GodotObject.IsInstanceValid(o);

        protected override void Members(MemberTable<GodotObject> m)
        {
            m.Add<ulong>("id", o => o.GetInstanceId(), description: "Godot instance id (the number in $ref)")
             .Add<string>("class", o => o.GetClass(), description: "Godot class name, e.g. \"OmniLight3D\" (GDExtension classes too)");
            m.Untyped.AddMixedMap("props",
                o => GodotVariants.Properties((GodotObject)o).Select(p => p["name"].AsString()).ToList(),
                (o, key) => EntryType((GodotObject)o, key),
                (o, key) => GodotVariants.ToClr(((GodotObject)o).Get(key)),
                (o, key, value) => Set((GodotObject)o, key, value),
                (o, key, value) => GodotVariants.Property((GodotObject)o, key) == null ? $"{((GodotObject)o).GetClass()} has no property '{key}'" : null,
                "every Godot property by its snake_case name (GDScript script variables and GDExtension properties included)",
                has: (o, key) => GodotVariants.Property((GodotObject)o, key) != null);
            m.Untyped.AddMethod("CallMethod",
                new[] { new CallableParameter("method", typeof(string)), new CallableParameter("args", typeof(List<object?>), hasDefault: true, defaultValue: null) },
                typeof(object),
                (o, a) => CallMethod((GodotObject)o, (string)a[0]!, a[1] as List<object?>),
                "call any Godot method (GDScript ones too) by name with a list of arguments; returns its result");
        }

        private static Type EntryType(GodotObject o, string key)
        {
            var info = GodotVariants.Property(o, key);
            if (info == null) return typeof(object);
            return GodotVariants.ClrType((Variant.Type)info["type"].AsInt64(), (PropertyHint)info["hint"].AsInt64(), info["hint_string"].AsString());
        }

        private static void Set(GodotObject o, string key, object? value)
        {
            var variant = GodotVariants.ToVariant(value);
            // A typed array property (Array[Font] …) refuses an untyped Array: fill a copy of the current, typed one
            // instead. An element of the wrong type is refused by Godot; that makes the whole write fail (rollback).
            var current = o.Get(key);
            if (current.VariantType == Variant.Type.Array && variant.VariantType == Variant.Type.Array)
            {
                var source = variant.AsGodotArray();
                var typed = current.AsGodotArray().Duplicate(false);
                typed.Clear();
                foreach (var element in source) typed.Add(element);
                if (typed.Count != source.Count)
                    throw new ArgumentException($"'{key}' is a typed array; an element does not match its element type");
                variant = typed;
            }
            o.Set(key, variant);
        }

        private static object? CallMethod(GodotObject o, string method, List<object?>? args)
        {
            if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("CallMethod needs a method name");
            if (!o.HasMethod(method)) throw new ArgumentException($"{o.GetClass()} has no method '{method}'");
            var variants = (args ?? new List<object?>()).Select(GodotVariants.ToVariant).ToArray();
            return GodotVariants.ToClr(o.Call(method, variants));
        }

        /// <summary>
        /// Text where a Godot object is expected: a <c>res://</c> / <c>uid://</c> path loads the resource, a node path or name
        /// finds the node in the edited scene (<c>godot.find</c> rules).
        /// </summary>
        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (!(value is string text) || text.Length == 0) return AdapterOutcome.NotHandled;
            var wantsResource = typeof(Resource).IsAssignableFrom(target) || target.IsAssignableFrom(typeof(Resource));
            var wantsNode = typeof(Node).IsAssignableFrom(target) || target.IsAssignableFrom(typeof(Node));
            if (wantsResource && (text.StartsWith("res://", StringComparison.Ordinal) || text.StartsWith("uid://", StringComparison.Ordinal)))
            {
                if (!RqlGodot.TryLoadResource(text, out var resource, out reason)) return AdapterOutcome.Failed;
                if (!target.IsInstanceOfType(resource)) { reason = $"'{text}' is a {resource!.GetClass()}, expected {target.Name}"; return AdapterOutcome.Failed; }
                result = resource;
                return AdapterOutcome.Ok;
            }
            if (wantsNode)
            {
                if (!RqlGodot.TryFindNode(text, out var node, out var error)) { reason = error!.Value.message; return AdapterOutcome.Failed; }
                if (!target.IsInstanceOfType(node)) { reason = $"node '{text}' is a {node!.GetClass()}, expected {target.Name}"; return AdapterOutcome.Failed; }
                result = node;
                return AdapterOutcome.Ok;
            }
            return AdapterOutcome.NotHandled;
        }
    }
}
#endif
