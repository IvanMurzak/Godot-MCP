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
using System.Collections;
using System.Globalization;
using System.Text.Json.Nodes;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Callable: shown as <c>"Player::_on_hit"</c> (<see cref="CallableText"/>: the target as a node path, res:// path or
    /// <c>Class#id</c>, then the method); members <c>target</c>, <c>method</c>, <c>valid</c>. Written as that text, a
    /// <c>[target, method]</c> list, an object literal <c>{target: …, method: …}</c> or <c>godot.callable(target, method)</c>;
    /// the target must exist and have the method (script methods included). A C# lambda reads as <c>"&lt;delegate&gt;"</c>; an
    /// empty Callable (no target, or a freed one) shows its members (<c>valid</c> false).
    /// </summary>
    public sealed class Godot_Callable_Adapter : RqlTypeAdapter<Callable>
    {
        public override string Description =>
            "Callable: \"<target>::<method>\" (target: node path, res:// path or Class#id), e.g. \"Player::_on_hit\"; " +
            "write that text, [target, method], {target, method} or godot.callable(target, method)";

        protected override JsonNode? Encode(Callable value, EncodeContext context) => Text(value) is { } text ? JsonValue.Create(text) : null;

        protected override void Members(MemberTable<Callable> m)
        {
            // The CLR members (Delegate, a function-pointer Trampoline) are plumbing; these three describe a Callable.
            m.HideWhere(_ => true);
            m.Add<GodotObject?>("target", c => TargetOf(c), description: "the object whose method it calls (null for a C# lambda or a freed object)")
             .Add<string>("method", MethodOf, description: "the method name")
             .Add<bool>("valid", c => c.Delegate != null || TargetOf(c) is { } t && MethodOf(c) is { Length: > 0 } method && RqlGodot.HasCallableMethod(t, method),
                 description: "true when the target exists and has the method");
        }

        protected override bool? Equal(Callable value, object? other, RqlKernel kernel) => other switch
        {
            string s => Text(value) == s.Trim(),
            Callable o => value.Delegate == null && o.Delegate == null && TargetOf(value) is { } a && TargetOf(o) is { } b
                          && a.GetInstanceId() == b.GetInstanceId() && MethodOf(value) == MethodOf(o),
            _ => null,
        };

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = null;
            reason = null;
            if (target != typeof(Callable)) return AdapterOutcome.NotHandled;
            if (value is Callable c) { result = c; return AdapterOutcome.Ok; }
            object? targetValue;
            object? method;
            switch (value)
            {
                case string text:
                    if (!CallableText.TryParse(text, out var t, out var m, out reason)) return AdapterOutcome.Failed;
                    (targetValue, method) = (t, m);
                    break;
                case ObjectValue literal:
                    literal.TryGetValue("target", out targetValue);
                    literal.TryGetValue("method", out method);
                    break;
                case IList list when list.Count == 2:
                    (targetValue, method) = (list[0], list[1]);
                    break;
                default:
                    reason = "expected \"<target>::<method>\", [target, method] or {target: …, method: …}";
                    return AdapterOutcome.Failed;
            }
            if (!TryMake(targetValue, method as string, out var made, out reason)) return AdapterOutcome.Failed;
            result = made;
            return AdapterOutcome.Ok;
        }

        /// <summary><c>godot.callable(target, method)</c>: a Callable of a node (path or node), resource (res://) or <c>Class#id</c>.</summary>
        public static object? Function(object?[] args)
        {
            if (args.Length != 2 || !(args[1] is string method))
                return new RqlError("type-mismatch", "godot.callable(target, method) expects a target (node path, node, res:// path or Class#id) and a method name");
            return TryMake(args[0], method, out var made, out var reason) ? made : new RqlError("not-found", reason);
        }

        /// <summary>The Callable of <paramref name="method"/> on the object <paramref name="target"/> names; the reason when there is none.</summary>
        public static bool TryMake(object? target, string? method, out Callable callable, out string? reason)
        {
            callable = default;
            reason = null;
            if (string.IsNullOrWhiteSpace(method)) { reason = "a Callable needs a method name"; return false; }
            var o = target as GodotObject;
            if (o == null)
            {
                if (!(target is string text) || text.Length == 0) { reason = "a Callable needs a target: a node path, a node, a res:// path or Class#id"; return false; }
                if (!TryResolve(text, out var resolved, out reason)) return false;
                o = resolved;
            }
            if (!GodotObject.IsInstanceValid(o)) { reason = "the Callable's target no longer exists"; return false; }
            if (!RqlGodot.HasCallableMethod(o, method))
            {
                reason = $"{o.GetClass()} has no method '{method}'";
                return false;
            }
            callable = new Callable(o, method);
            return true;
        }

        // A target's text: Class#id (the $ref form), a res:// / uid:// resource or a node of the edited scene; non-null when true.
        private static bool TryResolve(string text, out GodotObject o, out string? reason)
        {
            reason = null;
            GodotObject? found;
            var reference = CallableText.Reference.Match(text);
            if (reference.Success)
            {
                var type = reference.Groups["type"].Value;
                found = RqlGodot.ResolveStableRef(type, reference.Groups["id"].Value) as GodotObject;
                if (found == null) reason = $"no object {text}";
                // ResolveStableRef leaves the type check to the engine; a direct call checks it here (Godot class or C# type).
                else if (!IsOfType(found, type))
                {
                    reason = $"object {reference.Groups["id"].Value} is a {found.GetClass()}, not a {type}";
                    found = null;
                }
            }
            else if (text.StartsWith("res://", StringComparison.Ordinal) || text.StartsWith("uid://", StringComparison.Ordinal))
                found = RqlGodot.TryLoadResource(text, out var resource, out reason) ? resource : null;
            else if (RqlGodot.TryFindNode(text, out var node, out var error)) found = node;
            else (found, reason) = (null, error!.Value.message);
            o = found!;
            return found != null;
        }

        private static bool IsOfType(GodotObject o, string type)
        {
            if (o.IsClass(type)) return true;
            for (var t = o.GetType(); t != null; t = t.BaseType)
                if (t.Name == type) return true;
            return false;
        }

        /// <summary>
        /// The text of a Callable (<see cref="CallableText"/>), or null when its target is gone or it names no method (a custom
        /// Callable such as a GDScript lambda or a bound one, which C# sees as an empty Callable).
        /// </summary>
        public static string? Text(Callable c)
        {
            if (c.Delegate != null) return "<delegate>";
            return TargetOf(c) is { } target && MethodOf(c) is { Length: > 0 } method ? CallableText.Format(TargetText(target), method) : null;
        }

        private static GodotObject? TargetOf(Callable c) => c.Target is { } t && GodotObject.IsInstanceValid(t) ? t : null;

        // Method is null on default(Callable), which is also what C# makes of a custom (non-managed) native Callable.
        private static string MethodOf(Callable c) => c.Delegate != null ? "" : c.Method?.ToString() ?? "";

        private static string TargetText(GodotObject target) => target switch
        {
            Node node when RqlGodot.IsInEditedScene(node) => RqlGodot.PathOf(node),
            Resource resource when RqlGodot.IsFileResource(resource) => resource.ResourcePath,
            _ => $"{RqlHostNames.GodotClassName(target.GetType()) ?? target.GetType().Name}#{target.GetInstanceId().ToString(CultureInfo.InvariantCulture)}",
        };
    }
}
#endif
