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
            "Godot object: id, class, props[\"snake_name\"] (every Godot property, GDScript variables included), " +
            "layerNames[\"collision_mask\"] (layer properties by layer name)";

        protected override string? Id(GodotObject o) =>
            GodotObject.IsInstanceValid(o) ? o.GetInstanceId().ToString(CultureInfo.InvariantCulture) : null;

        protected override bool? Alive(GodotObject o) => GodotObject.IsInstanceValid(o);

        protected override void Members(MemberTable<GodotObject> m)
        {
            m.Add<ulong>("id", o => o.GetInstanceId(), description: "Godot instance id (the number in $ref)")
             .Add<string>("class", o => o.GetClass(), description: "Godot class name, e.g. \"OmniLight3D\" (GDExtension classes too)");
            m.Untyped.AddMixedMap("props",
                o => GodotVariants.Properties((GodotObject)o).Keys,
                // An enum-hinted int (grow_horizontal: "Begin,End,Both") also takes a name: typed object, converted on write.
                (o, key) => Property((GodotObject)o, key) is { } info ? (IsEnumInt(info) ? typeof(object) : info.ClrType) : typeof(object),
                (o, key) => GodotVariants.ToClr(((GodotObject)o).Get(key)),
                (o, key, value) => Set((GodotObject)o, key, EnumValue((GodotObject)o, key, value, out _)),
                (o, key, value) => PropProblem((GodotObject)o, key, value),
                "every Godot property by its snake_case name (GDScript script variables and GDExtension properties included)",
                // Script-only properties (rotation_degrees, global_position …) are reachable by name but not listed.
                has: (o, key) => Property((GodotObject)o, key) != null);
            m.Untyped.AddMixedMap("layerNames",
                o => Settings.RqlLayers.Properties((GodotObject)o),
                (o, key) => typeof(object),
                (o, key) => Settings.RqlLayers.Read((GodotObject)o, key),
                (o, key, value) => Settings.RqlLayers.Write((GodotObject)o, key, value),
                (o, key, value) => Settings.RqlLayers.Validate((GodotObject)o, key, value),
                "layer properties (collision_layer, collision_mask, visibility_layer, layers, cull_mask, navigation_layers …) as lists of " +
                "layer names, a number for an unnamed layer: set layerNames[\"collision_mask\"] = [\"world\", 3]",
                // Every key reaches validate, which names the object's layer properties.
                has: (o, key) => true);
            m.ExplicitOnly("layerNames");
            m.Untyped.AddMethod("CallMethod",
                new[] { new CallableParameter("method", typeof(string)), new CallableParameter("args", typeof(ArgumentList), hasDefault: true, defaultValue: null) },
                typeof(object),
                (o, a) => CallMethod((GodotObject)o, (string)a[0]!, a[1] as ArgumentList),
                "call any Godot method (GDScript ones too) by name with a list of arguments, each converted to the type the method " +
                "declares (\"(1, 2)\" for a Vector2i, \"res://…\" for a resource, an enum by name); returns its result",
                (o, a) => RqlGodot.Rejection(() => Prepare((GodotObject)o, (string)a[0]!, a[1] as ArgumentList, build: false)));
        }

        /// <summary>
        /// A new Godot object from an object literal naming its class — <c>{"$type": "BoxShape3D", Size: "(1, 2, 1)"}</c>, or
        /// <c>{class: "BoxShape3D", …}</c> (the member every Godot object reads as): an embedded sub-resource for a property
        /// (shape, mesh, material, environment …). A Godot class goes to RQL's own construction (C# members); a class Godot
        /// cannot instantiate is refused (C# would build a useless abstract Mesh); a script class (GDScript <c>class_name</c>)
        /// or a GDExtension class without a C# type is built here only when it is a Resource (a Node class is an engine object),
        /// its other keys written as Godot properties (snake_case or the C#-style name).
        /// </summary>
        private static AdapterOutcome TryBuild(ObjectValue literal, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            var key = literal.ContainsKey("$type") ? "$type" : literal.ContainsKey("class") ? "class" : null;
            if (key == null || !(literal[key] is string className) || className.Length == 0) return AdapterOutcome.NotHandled;
            if (ClassDB.ClassExists(className))
            {
                if (!ClassDB.CanInstantiate(className)) { reason = $"Godot class '{className}' cannot be instantiated (abstract); name a concrete class"; return AdapterOutcome.Failed; }
                // RQL builds it from "$type" with the C# type (CSGBox3D's is CsgBox3D, ORMMaterial3D's OrmMaterial3D). Rewritten in
                // place and left to RQL rather than built through the kernel here: a nested value of the same target type would
                // then skip this adapter (the kernel's re-entry guard) and lose res:// paths and nested literals.
                var clr = GodotTypes.GodotClassType(className);
                if (clr != null)
                {
                    literal.Remove("class");
                    literal["$type"] = clr.FullName;
                    // Godot property names ("albedo_color") name the C# members RQL builds from (AlbedoColor).
                    foreach (var k in literal.Keys.Where(k => k != "$type").ToList())
                        if (RqlHostNames.CSharpMember(clr, k) is { } member && !literal.ContainsKey(member))
                        {
                            literal[member] = literal[k];
                            literal.Remove(k);
                        }
                    return AdapterOutcome.NotHandled;
                }
                // A GDExtension class (Terrain3DAssets, Terrain3DTextureAsset …) has no C# type for RQL to build: a Resource is
                // built here like a script class, its other keys written as Godot properties.
            }
            else if (!RqlGodot.IsGlobalScriptClass(className)) return AdapterOutcome.NotHandled;
            // Only a script / GDExtension Resource is built from a literal: RQL refuses engine objects (a Node class included),
            // and an overload check runs this adapter without keeping its result, so a built Node would leak.
            if (RqlGodot.LiteralRefusal(literal, target) is { } refusal) { reason = refusal; return AdapterOutcome.Failed; }
            GodotObject built;
            try { built = RqlGodot.Instantiate<GodotObject>(className, "Resource", "class"); }
            catch (ArgumentException e) { reason = e.Message; return AdapterOutcome.Failed; }
            reason = target.IsInstanceOfType(built) ? SetScriptProperties(built, className, key, literal, kernel) : $"a {className} is not a {target.Name}";
            if (reason == null) { result = built; return AdapterOutcome.Ok; }
            if (!(built is RefCounted)) built.Free(); // a failed build of a Node / Object script class is not kept
            return AdapterOutcome.Failed;
        }

        // The literal's other keys as Godot properties of a script-class object (snake_case or the C#-style name); the problem.
        private static string? SetScriptProperties(GodotObject built, string className, string classKey, ObjectValue literal, RqlKernel kernel)
        {
            var props = GodotVariants.Properties(built);
            foreach (var kv in literal)
            {
                if (kv.Key == classKey) continue;
                var name = props.ContainsKey(kv.Key) ? kv.Key : kv.Key.ToSnakeCase();
                if (!props.TryGetValue(name, out var info)) return $"{className} has no property '{kv.Key}'";
                var value = EnumValue(built, name, kv.Value, out var problem);
                if (problem != null) return $"'{kv.Key}': {problem}";
                // A Godot-object property (a texture path, a nested literal) is coerced here first: through the kernel it could
                // skip this adapter (re-entry guard of the same target type).
                object? converted = null;
                string? why = null;
                var outcome = typeof(GodotObject).IsAssignableFrom(info.ClrType)
                    ? CoerceObject(value, info.ClrType, kernel, out converted, out why)
                    : AdapterOutcome.NotHandled;
                if (outcome == AdapterOutcome.NotHandled && !kernel.TryCoerce(value, info.ClrType, out converted, out why)) outcome = AdapterOutcome.Failed;
                if (outcome == AdapterOutcome.Failed) return $"'{kv.Key}': {why}";
                built.Set(name, GodotVariants.ToVariant(converted));
            }
            return null;
        }

        // A listed property (stored or in the inspector), else a script-only one.
        private static GodotVariants.GodotProperty? Property(GodotObject o, string key) =>
            GodotVariants.Properties(o).TryGetValue(key, out var info) ? info : GodotVariants.ScriptOnlyProperty(o, key);

        private static bool IsEnumInt(GodotVariants.GodotProperty info) => info.Type == Variant.Type.Int && info.Hint == PropertyHint.Enum;

        private static string? PropProblem(GodotObject o, string key, object? value)
        {
            EnumValue(o, key, value, out var problem);
            return problem;
        }

        // The value property `key` is written with: an enum-hinted int's name becomes its number; other values pass as given.
        // An unknown property, or an enum name / value that is not a number, sets `problem`.
        private static object? EnumValue(GodotObject o, string key, object? value, out string? problem)
        {
            problem = null;
            if (!(Property(o, key) is { } info)) { problem = $"{o.GetClass()} has no property '{key}'"; return value; }
            if (!IsEnumInt(info)) return value;
            switch (value)
            {
                case string number when long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed): return parsed;
                case string text:
                    var options = GodotEnumOptions.OfProperty(o, key, info.HintString);
                    if (GodotEnumNames.TryMatch(options, text, out var v)) return v;
                    problem = $"\"{text}\" is not {GodotEnumNames.Describe(options)}";
                    return value;
                case double d when d == Math.Floor(d) && !double.IsInfinity(d): return (long)d;
                case int or long or Enum: return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                default:
                    problem = $"expected a name ({GodotEnumNames.Describe(GodotEnumNames.ParseHint(info.HintString))}), got {value ?? "null"}";
                    return value;
            }
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

        private static object? CallMethod(GodotObject o, string method, ArgumentList? args) =>
            GodotVariants.ToClr(o.Call(method, Prepare(o, method, args)));

        // Every check of a call and its converted arguments; the call validator runs it for every target first (dry runs too),
        // with build false: object literals are not built there (a Node built per target would never be freed).
        private static Variant[] Prepare(GodotObject o, string method, ArgumentList? args, bool build = true)
        {
            if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("CallMethod needs a method name");
            if (!o.HasMethod(method)) throw new ArgumentException($"{o.GetClass()} has no method '{method}'");
            // In the editor a non-tool script runs as a placeholder: Godot answers a call to its methods with an error text
            // ("Attempt to call a method on a placeholder instance…") as if it were the result.
            if (!ClassDB.ClassHasMethod(o.GetClass(), method) && o.GetScript().As<Script>() is { } script && !script.IsTool())
                throw new InvalidOperationException($"'{method}' is a method of the non-tool script {script.ResourcePath}, which does not run in the editor; " +
                    "add @tool at the top of a GDScript (or [Tool] on a C# class) to call it here");
            var entry = GodotCallArguments.FindMethod(o, method);
            if (ArgumentProblem(entry, method, args?.Values.Count ?? 0) is { } problem) throw new ArgumentException(problem);
            return GodotCallArguments.Convert(entry, method, args, build);
        }

        // A wrong argument count makes Godot print an error and return null, which would read as a successful call.
        private static string? ArgumentProblem(global::Godot.Collections.Dictionary? entry, string method, int given)
        {
            if (entry == null || (entry["flags"].AsInt32() & (int)MethodFlags.Vararg) != 0) return null;
            var max = entry["args"].AsGodotArray().Count;
            var min = max - entry["default_args"].AsGodotArray().Count;
            return given >= min && given <= max ? null : $"{method} takes {(min == max ? $"{max}" : $"{min} to {max}")} argument(s), got {given}";
        }

        /// <summary>
        /// Text where a Godot object is expected: a <c>res://</c> / <c>uid://</c> path loads the resource, a node path or name
        /// finds the node in the edited scene (<c>godot.find</c> rules).
        /// </summary>
        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason) =>
            CoerceObject(value, target, kernel, out result, out reason);

        private static AdapterOutcome CoerceObject(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (value is ObjectValue literal) return TryBuild(literal, target, kernel, out result, out reason);
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
