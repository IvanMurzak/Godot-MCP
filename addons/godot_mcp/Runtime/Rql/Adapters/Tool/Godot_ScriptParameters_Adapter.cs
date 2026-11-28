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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Data;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// <c>csharp-exec</c> parameters as RQL values: named values <c>{n: 6, label: "x", target: ref("Node3D#12")}</c>, each a
    /// parameter of its value's type (a Godot object's own class, string, bool, a whole number → int or long, another
    /// number → double), or of the type written before its name, the value coerced to it like a <c>set</c>
    /// (<c>{"Vector3 at": [1, 2, 3], "Color tint": "#ff8800", "float speed": 2}</c>). The list form of earlier versions
    /// (<c>[{name, typeName, value}]</c>) works too. Pure-managed (a Godot object is only type-tested).
    /// </summary>
    public sealed class Godot_ScriptParameters_Adapter : RqlTypeAdapter<ScriptParameters>
    {
        public override string? Description =>
            "named values {n: 6, label: \"x\", target: ref(\"Node3D#12\"), \"Vector3 at\": [1, 2, 3]}: each becomes a parameter (a variable of " +
            "the snippet, or the method's argument in order) of the type written before its name, else of its value's type (a node or " +
            "resource, string, bool, a whole number → int, another number → double)";

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            var items = new List<ScriptParameter>();
            if (value is ObjectValue named)
            {
                foreach (var kv in named)
                {
                    if (!TryParameter(kv.Key, null, kv.Value, kernel, out var parameter, out reason)) return AdapterOutcome.Failed;
                    items.Add(parameter!);
                }
            }
            else if (value is List<object?> list && list.All(i => i is ObjectValue o && o.ContainsKey("name")))
            {
                foreach (var o in list.Cast<ObjectValue>())
                {
                    if (!(o["name"] is string name) || name.Length == 0) { reason = "every parameter needs a name"; return AdapterOutcome.Failed; }
                    var typeName = o.TryGetValue("typeName", out var t) ? t as string : null;
                    o.TryGetValue("value", out var v);
                    if (!TryParameter(name, string.IsNullOrWhiteSpace(typeName) ? null : typeName, v, kernel, out var parameter, out reason)) return AdapterOutcome.Failed;
                    items.Add(parameter!);
                }
            }
            else return AdapterOutcome.NotHandled;
            var duplicate = items.GroupBy(p => p.Name).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null) { reason = $"parameter '{duplicate.Key}' is given twice"; return AdapterOutcome.Failed; }
            result = new ScriptParameters(items);
            return AdapterOutcome.Ok;
        }

        private static bool TryParameter(string key, string? typeName, object? value, RqlKernel kernel, out ScriptParameter? parameter, out string? reason)
        {
            parameter = null;
            reason = null;
            var name = key.Trim();
            if (typeName == null)
            {
                var space = name.LastIndexOf(' ');
                if (space > 0)
                {
                    typeName = name.Substring(0, space).Trim();
                    name = name.Substring(space + 1);
                }
            }
            if (!IsIdentifier(name))
            {
                reason = $"'{key}' is not a parameter name (write \"name\" or \"Type name\", e.g. \"Vector3 at\")";
                return false;
            }
            if (typeName != null)
            {
                var type = ResolveType(typeName);
                if (type == null) { reason = $"parameter '{name}': type '{typeName}' not found"; return false; }
                if (!kernel.TryCoerce(value, type, out var coerced, out var why)) { reason = $"parameter '{name}': {why}"; return false; }
                parameter = new ScriptParameter(name, type, coerced);
                return true;
            }
            // RQL numbers are doubles: a whole one is passed as an integer.
            switch (value)
            {
                case null: parameter = new ScriptParameter(name, typeof(object), null); return true;
                case string s: parameter = new ScriptParameter(name, typeof(string), s); return true;
                case bool b: parameter = new ScriptParameter(name, typeof(bool), b); return true;
                case double d when Math.Floor(d) == d && d >= int.MinValue && d <= int.MaxValue: parameter = new ScriptParameter(name, typeof(int), (int)d); return true;
                case double d when Math.Floor(d) == d && Math.Abs(d) < 9e15: parameter = new ScriptParameter(name, typeof(long), (long)d); return true;
                case double d: parameter = new ScriptParameter(name, typeof(double), d); return true;
                case long l when l >= int.MinValue && l <= int.MaxValue: parameter = new ScriptParameter(name, typeof(int), (int)l); return true;
                case long l: parameter = new ScriptParameter(name, typeof(long), l); return true;
                case global::Godot.GodotObject o: parameter = new ScriptParameter(name, o.GetType(), o); return true;
            }
            reason = $"parameter '{name}': write its type before the name for {(value is ObjectValue ? "an object" : value is List<object?> ? "a list" : "this")} " +
                     $"value, e.g. \"Vector3 {name}\": [1, 2, 3] or \"int[] {name}\": [1, 2]";
            return false;
        }

        private static bool IsIdentifier(string name) =>
            name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_');

        private static readonly Dictionary<string, Type> Aliases = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["bool"] = typeof(bool), ["byte"] = typeof(byte), ["sbyte"] = typeof(sbyte), ["char"] = typeof(char), ["short"] = typeof(short),
            ["ushort"] = typeof(ushort), ["int"] = typeof(int), ["uint"] = typeof(uint), ["long"] = typeof(long), ["ulong"] = typeof(ulong),
            ["float"] = typeof(float), ["double"] = typeof(double), ["decimal"] = typeof(decimal), ["string"] = typeof(string), ["object"] = typeof(object),
        };

        /// <summary>
        /// A parameter type by the name an agent writes: a C# alias (<c>int</c>, <c>float</c>), a full or assembly-qualified name,
        /// or a short name of a Godot or project type (<c>Vector3</c>, <c>Node3D</c>); <c>T[]</c> and <c>T?</c> too. Null when unknown.
        /// </summary>
        internal static Type? ResolveType(string name)
        {
            name = name.Trim();
            if (name.EndsWith("[]", StringComparison.Ordinal)) return ResolveType(name.Substring(0, name.Length - 2))?.MakeArrayType();
            if (name.EndsWith("?", StringComparison.Ordinal))
                return ResolveType(name.Substring(0, name.Length - 1)) is Type inner ? inner.IsValueType ? typeof(Nullable<>).MakeGenericType(inner) : inner : null;
            if (Aliases.TryGetValue(name, out var alias)) return alias;
            if (Type.GetType(name, throwOnError: false) is Type direct) return direct;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? found;
                try { found = assembly.GetType(name, throwOnError: false); }
                catch (Exception) { continue; } // an assembly that cannot list its types
                if (found != null) return found;
            }
            return GodotTypes.Candidates(name).FirstOrDefault();
        }
    }
}
