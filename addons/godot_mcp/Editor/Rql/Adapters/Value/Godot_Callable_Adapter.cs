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
             .Add<string>("method", c => c.Delegate != null ? "" : c.Method.ToString(), description: "the method name")
             .Add<bool>("valid", c => c.Delegate != null || TargetOf(c) is { } t && t.HasMethod(c.Method), description: "true when the target exists and has the method");
        }

        protected override bool? Equal(Callable value, object? other, RqlKernel kernel) => other switch
        {
            string s => Text(value) == s.Trim(),
            Callable o => Text(value) == Text(o),
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
                if (!TryResolve(text, out o, out reason)) return false;
            }
            if (o == null || !GodotObject.IsInstanceValid(o)) { reason = "the Callable's target no longer exists"; return false; }
            if (!o.HasMethod(method))
            {
                reason = $"{o.GetClass()} has no method '{method}'";
                return false;
            }
            callable = new Callable(o, method);
            return true;
        }

        // A target's text: Class#id, a res:// / uid:// resource or a node of the edited scene.
        private static bool TryResolve(string text, out GodotObject? o, out string? reason)
        {
            o = null;
            reason = null;
            var reference = CallableText.Reference.Match(text);
            if (reference.Success)
            {
                if (ulong.TryParse(reference.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && GodotObject.IsInstanceIdValid(id))
                    o = GodotObject.InstanceFromId(id);
                if (o == null) reason = $"no object {text}";
                return o != null;
            }
            if (text.StartsWith("res://", StringComparison.Ordinal) || text.StartsWith("uid://", StringComparison.Ordinal))
            {
                var ok = RqlGodot.TryLoadResource(text, out var resource, out reason);
                o = resource;
                return ok;
            }
            if (RqlGodot.TryFindNode(text, out var node, out var error)) { o = node; return true; }
            reason = error!.Value.message;
            return false;
        }

        /// <summary>The text of a Callable (<see cref="CallableText"/>), or null when its target is gone.</summary>
        public static string? Text(Callable c)
        {
            if (c.Delegate != null) return "<delegate>";
            return TargetOf(c) is { } target ? CallableText.Format(TargetText(target), c.Method.ToString()) : null;
        }

        private static GodotObject? TargetOf(Callable c) => c.Target is { } t && GodotObject.IsInstanceValid(t) ? t : null;

        private static string TargetText(GodotObject target) => target switch
        {
            Node node when RqlGodot.IsInEditedScene(node) => RqlGodot.PathOf(node),
            Resource resource when RqlGodot.IsFileResource(resource) => resource.ResourcePath,
            _ => $"{target.GetType().Name}#{target.GetInstanceId().ToString(CultureInfo.InvariantCulture)}",
        };
    }
}
#endif
