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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Bulk create behind <c>editor.CreateNode(class, {…})</c> and <c>editor.InstantiateScene(path, {…})</c>: RQL runs the call once per
    /// stream item (<c>range(0, 300) | call editor.CreateNode(…)</c>), checks every item first (class or scene, parent, name, each
    /// property and its value), and the nodes join the mutation's one undo action through <see cref="Attach"/>.
    /// </summary>
    public static partial class RqlGodot
    {
        // The property list of each class / scene a running mutation creates (one throwaway instance per class or scene, not one
        // per item): name → the list entry (type, hint, hint_string, class_name, usage). Cleared with the mutation's state.
        private static readonly Dictionary<string, Dictionary<string, global::Godot.Collections.Dictionary>> _propertyTables =
            new Dictionary<string, Dictionary<string, global::Godot.Collections.Dictionary>>(StringComparer.Ordinal);

        /// <summary>A new node of <paramref name="className"/> with <paramref name="options"/>, added to the edited scene.</summary>
        public static Node CreateNode(string className, NodeOptions? options)
        {
            var root = RequireEditedRoot();
            var parent = NewNodeParent(options, root);
            var node = Instantiate(className);
            return AttachNew(node, parent, root, options, "class:" + className);
        }

        /// <summary>A new instance of the scene at <paramref name="path"/> with <paramref name="options"/>, added to the edited scene.</summary>
        public static Node InstantiateSceneWith(string path, NodeOptions? options)
        {
            var root = RequireEditedRoot();
            var parent = NewNodeParent(options, root);
            var node = InstantiableScene(path, root).Instantiate(PackedScene.GenEditState.Instance)
                       ?? throw new InvalidOperationException($"could not instantiate '{path}'");
            return AttachNew(node, parent, root, options, "scene:" + path);
        }

        public static string? ValidateCreateNode(string className, NodeOptions? options) => Rejection(() =>
        {
            var root = RequireEditedRoot();
            var parent = NewNodeParent(options, root);
            NodeClass(className, out _);
            CheckNewNode(parent, options, "class:" + className, () => Instantiate(className));
        });

        public static string? ValidateInstantiateSceneWith(string path, NodeOptions? options) => Rejection(() =>
        {
            var root = RequireEditedRoot();
            var parent = NewNodeParent(options, root);
            var packed = InstantiableScene(path, root);
            CheckNewNode(parent, options, "scene:" + path, () => packed.Instantiate(PackedScene.GenEditState.Instance)
                ?? throw new InvalidOperationException($"could not instantiate '{path}'"));
        });

        // The batch rule of a bulk create (after every item passed its own check): two items may not give the same name under
        // one parent (Godot would rename the second one, and the receipt would not show the name asked for).
        private static string? ValidateCreateBatch(IReadOnlyList<MutationRecord> records)
        {
            var planned = new List<(object parent, string? name)>();
            var root = EditedRoot();
            foreach (var record in records)
            {
                if (!(record.Item is RqlEditorApi) || !IsCreateCall(record.Method?.Name) || record.Args.Count < 2) continue;
                var options = record.Args[1] as NodeOptions;
                if (options?.Name == null || root == null) continue;
                planned.Add((NewNodeParent(options, root), options.Name));
            }
            return NodeOptions.DuplicateName(planned) is { } name
                ? $"several items are named '{name}' under the same parent; give each a different name, e.g. \"{name}\" + $"
                : null;
        }

        private static bool IsCreateCall(string? method) =>
            method == nameof(RqlEditorApi.CreateNode) || method == nameof(RqlEditorApi.InstantiateScene);

        // The parent a new node goes under: the options' parent (a node, a path, a ref) or the scene root.
        private static Node NewNodeParent(NodeOptions? options, Node root)
        {
            if (options?.Parent == null) return root;
            var parent = options.Parent as Node;
            if (parent == null)
            {
                if (options.Kernel == null) throw new ArgumentException("parent: expected a node");
                if (!options.Kernel.TryCoerce(options.Parent, typeof(Node), out var coerced, out var why))
                    throw new ArgumentException($"parent: {why}");
                parent = (Node)coerced!;
            }
            if (!GodotObject.IsInstanceValid(parent)) throw new ArgumentException("parent: the node was freed");
            RequireInEditedScene(parent, "parent");
            return parent;
        }

        // Every check of a new node that reads only: its name among the parent's children, and each property (it exists on the
        // class or scene, its value converts). Object literals of non-resource values are built only by the call itself.
        private static void CheckNewNode(Node parent, NodeOptions? options, string tableKey, Func<Node> instantiate)
        {
            if (options == null) return;
            if (options.Name != null && NodeNameRules.Problem(options.Name, n => parent.GetNodeOrNull(n) != null) is { } problem)
                throw new ArgumentException($"name: {problem}");
            var table = PropertyTable(tableKey, instantiate);
            foreach (var kv in options.Properties)
            {
                var info = PropertyEntry(table, kv.Key, tableKey);
                if (kv.Value is IDictionary<string, object?> && (Variant.Type)info.Value["type"].AsInt32() == Variant.Type.Object
                    && !typeof(Resource).IsAssignableFrom(GodotTypes.GodotObjectType(info.Value["class_name"].AsString())))
                    continue;
                ConvertProperty(kv.Value, info, options);
            }
        }

        // Name, properties, then into the scene (owned by the root, one undo step with the rest of the mutation). A property that
        // fails frees the node before it joins the scene.
        private static Node AttachNew(Node node, Node parent, Node root, NodeOptions? options, string tableKey)
        {
            try
            {
                if (options?.Name != null)
                {
                    if (NodeNameRules.Problem(options.Name, n => parent.GetNodeOrNull(n) != null) is { } problem)
                        throw new ArgumentException($"name: {problem}");
                    node.Name = options.Name;
                }
                if (options != null && options.Properties.Count > 0)
                {
                    var table = PropertyTable(tableKey, () => node, keep: true);
                    foreach (var kv in options.Properties)
                    {
                        var info = PropertyEntry(table, kv.Key, tableKey);
                        node.Set(info.Key, ConvertProperty(kv.Value, info, options));
                    }
                }
            }
            catch
            {
                node.Free();
                throw;
            }
            EnsureEditable(parent, root);
            return Attach(parent, node, root);
        }

        private static Variant ConvertProperty(object? value, KeyValuePair<string, global::Godot.Collections.Dictionary> info, NodeOptions options)
        {
            if (options.Kernel == null) return GodotVariants.ToVariant(value);
            return GodotCallArguments.ConvertTo(value, info.Value, options.Kernel, info.Key);
        }

        // The property list of a class or scene, read from one instance (freed unless the caller keeps it) and cached for the
        // running mutation.
        private static Dictionary<string, global::Godot.Collections.Dictionary> PropertyTable(string key, Func<Node> instantiate, bool keep = false)
        {
            if (_propertyTables.TryGetValue(key, out var table)) return table;
            var node = instantiate();
            try
            {
                table = new Dictionary<string, global::Godot.Collections.Dictionary>(StringComparer.Ordinal);
                foreach (var entry in node.GetPropertyList())
                    if (!GodotVariants.IsHeader((PropertyUsageFlags)entry["usage"].AsInt64()))
                        table[entry["name"].AsString()] = entry;
            }
            finally
            {
                if (!keep) node.Free();
            }
            _propertyTables[key] = table;
            return table;
        }

        // The list entry of an option key: the Godot name (snake_case) or the C#-style one (Position, RotationDegrees).
        private static KeyValuePair<string, global::Godot.Collections.Dictionary> PropertyEntry(
            Dictionary<string, global::Godot.Collections.Dictionary> table, string key, string tableKey)
        {
            if (table.TryGetValue(key, out var entry)) return new KeyValuePair<string, global::Godot.Collections.Dictionary>(key, entry);
            var snake = key.ToSnakeCase();
            if (table.TryGetValue(snake, out entry)) return new KeyValuePair<string, global::Godot.Collections.Dictionary>(snake, entry);
            var what = tableKey.StartsWith("scene:", StringComparison.Ordinal) ? $"the root of {tableKey.Substring(6)}" : $"a {tableKey.Substring(6)}";
            throw new ArgumentException($"{what} has no property '{key}'" + Similar(table.Keys, key) +
                $"; the options are {NodeOptions.NameKey}, {NodeOptions.ParentKey} and Godot properties (snake_case, as in props[\"…\"])");
        }
    }
}
#endif
