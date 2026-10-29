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
        // class or scene, its value converts). An object literal for a non-resource property is refused here (RQL never builds
        // an engine object from a literal), so a dry run fails like the call.
        private static void CheckNewNode(Node parent, NodeOptions? options, string tableKey, Func<Node> instantiate)
        {
            if (options == null) return;
            CheckNewName(parent, options.Name);
            var table = PropertyTable(tableKey, instantiate);
            foreach (var kv in options.Properties)
            {
                var info = PropertyEntry(table, kv.Key, tableKey);
                if (kv.Value is IDictionary<string, object?> literal && (Variant.Type)info.Value["type"].AsInt32() == Variant.Type.Object)
                {
                    var target = GodotCallArguments.ObjectType(GodotCallArguments.ObjectClasses(info.Value["class_name"].AsString()));
                    if (!typeof(Resource).IsAssignableFrom(target))
                    {
                        if (LiteralRefusal(literal, target) is { } refusal) throw new ArgumentException($"{kv.Key}: {refusal}");
                        continue; // a literal naming a resource class for a property typed Object: built by the call
                    }
                }
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
                    CheckNewName(parent, options.Name);
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

        // A name Godot would change (a character it does not allow, a sibling's name) is refused, like a rename.
        private static void CheckNewName(Node parent, string? name)
        {
            if (name == null) return;
            if (parent.GetNodeOrNull(name) != null)
                throw new ArgumentException($"name: '{PathOf(parent)}' already has a child named '{name}'; pick another name");
            if (NodeNameRules.Problem(name, _ => false) is { } problem)
                throw new ArgumentException($"name: {problem}");
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
            var name = table.ContainsKey(key) ? key : key.ToSnakeCase();
            // A global transform set before the node has a parent would be taken as its local one; a script set here would
            // replace the C# wrapper the call still holds.
            if (name.StartsWith("global_", StringComparison.Ordinal))
                throw new ArgumentException($"'{key}' cannot be set on a node being created (it has no parent yet); use '{name.Substring(7)}', which is relative to the parent");
            if (name == "script")
                throw new ArgumentException("'script' cannot be set on a node being created; create the script class itself (CreateNode(\"<class_name>\")) or attach the script afterwards with the script-attach-to-node tool");
            if (table.TryGetValue(name, out var entry)) return new KeyValuePair<string, global::Godot.Collections.Dictionary>(name, entry);
            var what = tableKey.StartsWith("scene:", StringComparison.Ordinal) ? $"the root of {tableKey.Substring(6)}" : $"a {tableKey.Substring(6)}";
            throw new ArgumentException($"{what} has no property '{key}'" + Similar(table.Keys, key) +
                $"; the options are {NodeOptions.NameKey}, {NodeOptions.ParentKey} and Godot properties of a new {tableKey.Substring(6)} (snake_case, " +
                "as in props[\"…\"]); a property that appears only once another one is set (a mesh's surface materials) takes a set on the returned nodes");
        }
    }
}
#endif
