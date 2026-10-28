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
using System.Collections.Generic;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The options of <c>editor.CreateNode(class, {…})</c> / <c>editor.InstantiateScene(path, {…})</c> as written: <c>name</c>,
    /// <c>parent</c> (a node, a path or a ref; null = the scene root) and every other key a Godot property of the new node
    /// (<c>position</c>, <c>mesh</c>, <c>rotation_degrees</c> …). The values stay raw, with the kernel that coerced the literal, so
    /// each one is converted like a <c>set</c> would once the node's class tells its type. Pure (unit-tested).
    /// </summary>
    public sealed class NodeOptions
    {
        public const string NameKey = "name";
        public const string ParentKey = "parent";

        internal NodeOptions(string? name, object? parent, IReadOnlyList<KeyValuePair<string, object?>> properties, RqlKernel? kernel)
        {
            Name = name;
            Parent = parent;
            Properties = properties;
            Kernel = kernel;
        }

        /// <summary>The node's name; null = Godot's default (the class, or the scene root's name).</summary>
        public string? Name { get; }

        /// <summary>The parent as written (a node, a node path, a ref); null = the edited scene root.</summary>
        public object? Parent { get; }

        /// <summary>Godot properties to set on the new node, in the order written.</summary>
        public IReadOnlyList<KeyValuePair<string, object?>> Properties { get; }

        internal RqlKernel? Kernel { get; }

        /// <summary>Split an object literal into name, parent and properties; the problem when it is not one.</summary>
        public static NodeOptions? From(object? value, RqlKernel? kernel, out string? problem)
        {
            problem = null;
            if (!(value is IDictionary<string, object?> literal))
            {
                problem = "expected an object literal of options, e.g. {name: \"Crate\", parent: \"Props\", position: [0, 1, 0]}";
                return null;
            }
            string? name = null;
            object? parent = null;
            var properties = new List<KeyValuePair<string, object?>>();
            foreach (var kv in literal)
            {
                if (string.Equals(kv.Key, NameKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Value != null && !(kv.Value is string)) { problem = $"'name' must be a string, e.g. \"Crate\" + $"; return null; }
                    name = (string?)kv.Value;
                }
                else if (string.Equals(kv.Key, ParentKey, StringComparison.OrdinalIgnoreCase)) parent = kv.Value;
                else properties.Add(kv);
            }
            return new NodeOptions(name, parent, properties, kernel);
        }

        /// <summary>
        /// The first name two items of one bulk create give under the same parent (Godot would rename the second one),
        /// or null. <paramref name="planned"/> holds (parent key, name) per item; unnamed items are skipped.
        /// </summary>
        public static string? DuplicateName(IEnumerable<(object parent, string? name)> planned)
        {
            var seen = new HashSet<(object, string)>();
            foreach (var (parent, name) in planned)
                if (name != null && !seen.Add((parent, name))) return name;
            return null;
        }
    }

    /// <summary>Takes an object literal as <see cref="NodeOptions"/>.</summary>
    public sealed class Godot_NodeOptions_Adapter : RqlTypeAdapter<NodeOptions>
    {
        public override string Description => "options of a new node: {name: \"Crate\", parent: \"Props\", position: [0, 1, 0], <any Godot property>: …}";

        protected override void Members(MemberTable<NodeOptions> m) { }

        public override AdapterOutcome TryCoerce(object? value, Type target, RqlKernel kernel, out object? result, out string? reason)
        {
            result = NodeOptions.From(value, kernel, out reason);
            return result != null ? AdapterOutcome.Ok : AdapterOutcome.Failed;
        }
    }
}
#endif
