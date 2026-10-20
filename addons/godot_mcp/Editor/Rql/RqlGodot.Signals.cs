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
using Godot;
using Rql;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Signal connections and groups of the edited scene's nodes — what the editor's Node dock (Signals / Groups tabs)
    /// edits. Connections are persistent (<see cref="GodotObject.ConnectFlags.Persist"/>, saved in the .tscn as
    /// <c>[connection …]</c>) and groups are persistent groups (saved as <c>groups=[…]</c> in the node header). Both are not
    /// Godot properties, so each change registers its own do/undo methods and marks the scene unsaved.
    /// </summary>
    public static partial class RqlGodot
    {
        // ------------------------------------------------------------ signals

        /// <summary>The signals <paramref name="o"/> can emit (its class's and its script's), by name.</summary>
        public static List<RqlSignal> SignalsOf(GodotObject o) =>
            o.GetSignalList().Select(s => new RqlSignal(o, s)).OrderBy(s => s.name, StringComparer.Ordinal).ToList();

        /// <summary>The persistent connections from the signals of <paramref name="node"/> (what the Node dock lists and the scene saves).</summary>
        public static List<RqlConnection> ConnectionsOf(Node node)
        {
            var list = new List<RqlConnection>();
            foreach (var signal in node.GetSignalList())
            {
                var name = signal["name"].AsStringName();
                foreach (var c in node.GetSignalConnectionList(name))
                {
                    var flags = c["flags"].AsInt64();
                    if ((flags & (long)GodotObject.ConnectFlags.Persist) == 0) continue;
                    list.Add(new RqlConnection(node, name.ToString(), c["callable"].AsCallable(), flags));
                }
            }
            return list;
        }

        /// <summary>The persistent connections whose method is on <paramref name="node"/>.</summary>
        public static List<RqlConnection> IncomingConnectionsOf(Node node)
        {
            var list = new List<RqlConnection>();
            foreach (var c in node.GetIncomingConnections())
            {
                var flags = c["flags"].AsInt64();
                if ((flags & (long)GodotObject.ConnectFlags.Persist) == 0) continue;
                var signal = c["signal"].AsSignal();
                if (signal.Owner is Node source)
                    list.Add(new RqlConnection(source, signal.Name.ToString(), c["callable"].AsCallable(), flags));
            }
            return list;
        }

        /// <summary>Connect a signal of <paramref name="source"/> to a method of <paramref name="target"/> (persistent, undoable).</summary>
        public static RqlConnection Connect(Node source, string signal, Node target, string method, bool deferred, bool oneShot)
        {
            RequireEditedRoot();
            RequireInEditedScene(source, "source");
            RequireInEditedScene(target, "target");
            if (string.IsNullOrWhiteSpace(signal)) throw new ArgumentException("a signal name is required, e.g. \"pressed\"");
            if (!source.HasSignal(signal))
                throw new ArgumentException($"{source.GetClass()} '{source.Name}' has no signal '{signal}'" + Similar(SignalsOf(source).Select(s => s.name), signal));
            if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("a method name is required, e.g. \"_on_button_pressed\"");
            if (!HasCallableMethod(target, method))
                throw new ArgumentException($"{target.GetClass()} '{target.Name}' has no method '{method}': add it to the target's script first " +
                                            "(script-update), then connect");
            var callable = new Callable(target, method);
            if (source.IsConnected(signal, callable))
                throw new InvalidOperationException($"'{source.Name}'.{signal} is already connected to '{target.Name}'.{method}");
            var flags = (uint)GodotObject.ConnectFlags.Persist
                        | (deferred ? (uint)GodotObject.ConnectFlags.Deferred : 0)
                        | (oneShot ? (uint)GodotObject.ConnectFlags.OneShot : 0);
            var error = source.Connect(signal, callable, flags);
            if (error != Error.Ok) throw new InvalidOperationException($"connecting '{source.Name}'.{signal} failed: {error}");
            RecordUndo(
                ur => ur.AddDoMethod(source, GodotObject.MethodName.Connect, signal, callable, flags),
                ur => ur.AddUndoMethod(source, GodotObject.MethodName.Disconnect, signal, callable));
            MarkSceneUnsaved();
            return new RqlConnection(source, signal, callable, flags);
        }

        /// <summary>Disconnect a connection made with <see cref="Connect"/> (or in the editor), undoably.</summary>
        public static void Disconnect(Node source, string signal, GodotObject target, string method)
        {
            RequireEditedRoot();
            RequireInEditedScene(source, "source");
            var callable = new Callable(target, method);
            var existing = source.HasSignal(signal) && source.IsConnected(signal, callable)
                ? source.GetSignalConnectionList(signal).FirstOrDefault(c => c["callable"].AsCallable().Equals(callable))
                : null;
            if (existing == null || (existing["flags"].AsInt64() & (long)GodotObject.ConnectFlags.Persist) == 0)
            {
                var targetName = target is Node n ? n.Name.ToString() : target.GetClass();
                throw new ArgumentException($"'{source.Name}'.{signal} is not connected to '{targetName}'.{method}");
            }
            var flags = (uint)existing["flags"].AsInt64();
            source.Disconnect(signal, callable);
            RecordUndo(
                ur => ur.AddDoMethod(source, GodotObject.MethodName.Disconnect, signal, callable),
                ur => ur.AddUndoMethod(source, GodotObject.MethodName.Connect, signal, callable, flags));
            MarkSceneUnsaved();
        }

        // A method the target (or its script, also a non-tool one whose instance is a placeholder) defines.
        internal static bool HasCallableMethod(GodotObject target, string method)
        {
            if (target.HasMethod(method)) return true;
            if (target.GetScript().As<Script>() is { } script)
                for (var s = script; s != null; s = s.GetBaseScript())
                    if (s.GetScriptMethodList().Any(m => m["name"].AsString() == method)) return true;
            return false;
        }

        /// <summary>" (similar: a, b)" for the names close to <paramref name="key"/> (containing it, or a typo away), or "".</summary>
        internal static string Similar(IEnumerable<string> names, string key)
        {
            var similar = names.Where(n => n.Contains(key, StringComparison.OrdinalIgnoreCase) || key.Contains(n, StringComparison.OrdinalIgnoreCase)
                                           || EditDistance(n.ToLowerInvariant(), key.ToLowerInvariant()) <= 2).Take(5).ToList();
            return similar.Count > 0 ? $" (similar: {string.Join(", ", similar)})" : "";
        }

        private static int EditDistance(string a, string b)
        {
            if (Math.Abs(a.Length - b.Length) > 2) return 3;
            var row = Enumerable.Range(0, b.Length + 1).ToArray();
            for (int i = 1; i <= a.Length; i++)
            {
                var diagonal = row[0];
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    var above = row[j];
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                    diagonal = above;
                }
            }
            return row[b.Length];
        }

        // ------------------------------------------------------------ groups

        /// <summary>
        /// The persistent groups of <paramref name="node"/> (what the scene saves and the Groups dock shows; internal "_…"
        /// groups excluded), sorted. A group a script joined at edit time without <c>persistent</c> is not listed.
        /// </summary>
        public static List<string> GroupsOf(Node node)
        {
            var groups = VisibleGroups(node);
            if (groups.Count == 0) return groups;
            var persistent = PersistentGroups(node);
            return persistent == null ? groups : groups.Where(persistent.Contains).ToList();
        }

        private static List<string> VisibleGroups(Node node) =>
            node.GetGroups().Select(g => g.ToString()).Where(g => g.Length > 0 && !g.StartsWith("_", StringComparison.Ordinal))
                .OrderBy(g => g, StringComparer.Ordinal).ToList();

        // A node's persistent groups cost a pack (of the whole scene for its root), so a query packs each node once:
        // RunQuery scopes this cache to one query; AddToGroup / RemoveFromGroup and the undo test hooks invalidate it.
        private static Dictionary<ulong, HashSet<string>?>? _persistentGroups;

        /// <summary>Run <paramref name="run"/> (one RQL query) with the persistent-groups cache; nested queries share it.</summary>
        internal static T RunQuery<T>(Func<T> run)
        {
            if (_persistentGroups != null) return run();
            _persistentGroups = new Dictionary<ulong, HashSet<string>?>();
            try { return run(); }
            finally { _persistentGroups = null; }
        }

        internal static void InvalidateGroups(Node? node = null)
        {
            if (node == null) _persistentGroups?.Clear();
            else _persistentGroups?.Remove(node.GetInstanceId());
        }

        /// <summary>
        /// The groups of <paramref name="node"/> a saved scene keeps, or null when that cannot be told (all count as
        /// persistent). Godot's API has no per-group persistent flag, but packing keeps exactly the persistent ones: the node
        /// packed on its own (a scene of the node and the descendants it owns — only itself, unless it is the scene root)
        /// lists them as its scene state's groups.
        /// </summary>
        private static HashSet<string>? PersistentGroups(Node node)
        {
            var id = node.GetInstanceId();
            if (_persistentGroups != null && _persistentGroups.TryGetValue(id, out var cached)) return cached;
            using var scene = new PackedScene();
            var state = scene.Pack(node) == Error.Ok ? scene.GetState() : null;
            var groups = state == null || state.GetNodeCount() == 0 ? null : new HashSet<string>(state.GetNodeGroups(0), StringComparer.Ordinal);
            if (_persistentGroups != null) _persistentGroups[id] = groups;
            return groups;
        }

        private static bool IsPersistentGroup(Node node, string group) =>
            node.IsInGroup(group) && (PersistentGroups(node)?.Contains(group) ?? true);

        /// <summary>The nodes of the edited scene in the persistent group <paramref name="group"/>, depth-first.</summary>
        public static List<Node> NodesInGroup(string group) => AllNodes().Where(n => IsPersistentGroup(n, group)).ToList();

        /// <summary><c>godot.group("enemies")</c>: the nodes of the edited scene in a group.</summary>
        public static object? FindGroup(object?[] args)
        {
            if (args.Length != 1 || !(args[0] is string name) || name.Length == 0)
                return new RqlError("type-mismatch", "godot.group(name) expects a group name such as \"enemies\"");
            return NodesInGroup(name);
        }

        /// <summary>
        /// Add <paramref name="node"/> to a persistent group (saved with the scene); false when it already was in it. A node
        /// in the group only at edit time (joined without <c>persistent</c>) is made a persistent member.
        /// </summary>
        public static bool AddToGroup(Node node, string group)
        {
            RequireInEditedScene(node, "node");
            Settings.RqlProject.RequireGroupName(group);
            var member = node.IsInGroup(group);
            if (member && IsPersistentGroup(node, group)) return false;
            // Godot keeps the flag of the first AddToGroup: a non-persistent membership has to be replaced.
            if (member) node.RemoveFromGroup(group);
            node.AddToGroup(group, persistent: true);
            InvalidateGroups(node);
            RecordUndo(
                ur =>
                {
                    if (member) ur.AddDoMethod(node, Node.MethodName.RemoveFromGroup, group);
                    ur.AddDoMethod(node, Node.MethodName.AddToGroup, group, true);
                },
                ur =>
                {
                    ur.AddUndoMethod(node, Node.MethodName.RemoveFromGroup, group);
                    if (member) ur.AddUndoMethod(node, Node.MethodName.AddToGroup, group, false);
                });
            MarkSceneUnsaved();
            return true;
        }

        /// <summary>Remove <paramref name="node"/> from a group; false when it was not in it.</summary>
        public static bool RemoveFromGroup(Node node, string group)
        {
            RequireInEditedScene(node, "node");
            if (!node.IsInGroup(group)) return false;
            var persistent = IsPersistentGroup(node, group);
            node.RemoveFromGroup(group);
            InvalidateGroups(node);
            RecordUndo(
                ur => ur.AddDoMethod(node, Node.MethodName.RemoveFromGroup, group),
                ur => ur.AddUndoMethod(node, Node.MethodName.AddToGroup, group, persistent));
            MarkSceneUnsaved();
            return true;
        }

        /// <summary>
        /// Make the persistent groups of <paramref name="node"/> exactly <paramref name="groups"/> (internal groups and the
        /// unlisted edit-time ones are kept; a listed edit-time group becomes persistent).
        /// </summary>
        public static void SetGroups(Node node, IEnumerable<string> groups)
        {
            var wanted = groups.Select(g => g.Trim()).Where(g => g.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            foreach (var g in wanted) Settings.RqlProject.RequireGroupName(g);
            foreach (var g in GroupsOf(node).Where(g => !wanted.Contains(g))) RemoveFromGroup(node, g);
            foreach (var g in wanted) AddToGroup(node, g);
        }
    }

    /// <summary>A signal an object can emit.</summary>
    public sealed class RqlSignal
    {
        private readonly GodotObject _owner;

        internal RqlSignal(GodotObject owner, global::Godot.Collections.Dictionary info)
        {
            _owner = owner;
            name = info["name"].AsString();
            args = info["args"].AsGodotArray().Select(a => a.AsGodotDictionary()).Select(Arg).ToList();
        }

        [RqlDescription("signal name, e.g. \"pressed\"")]
        public string name { get; }

        [RqlDescription("the arguments it is emitted with, as \"name: Type\"")]
        public List<string> args { get; }

        [RqlDescription("number of persistent connections from it (the ones saved with the scene)")]
        public int connections => _owner.GetSignalConnectionList(name).Count(c => (c["flags"].AsInt64() & (long)GodotObject.ConnectFlags.Persist) != 0);

        private static string Arg(global::Godot.Collections.Dictionary a)
        {
            var type = (Variant.Type)a["type"].AsInt64();
            var cls = a.TryGetValue("class_name", out var c) ? c.AsString() : "";
            var typeName = cls.Length > 0 ? cls : type == Variant.Type.Nil ? "Variant" : type.ToString();
            return $"{a["name"].AsString()}: {typeName}";
        }
    }

    /// <summary>A persistent signal connection (signal → target method), as the Node dock shows it.</summary>
    public sealed class RqlConnection
    {
        private readonly Callable _callable;

        internal RqlConnection(Node source, string signal, Callable callable, long flags)
        {
            this.source = source;
            this.signal = signal;
            _callable = callable;
            this.flags = flags;
        }

        [RqlDescription("node whose signal it is")]
        public Node source { get; }

        [RqlDescription("signal name")]
        public string signal { get; }

        [RqlDescription("object whose method is called (usually a node of the scene)")]
        public GodotObject? target => _callable.Target;

        [RqlDescription("method called on the target; empty for a custom callable (e.g. one with bound arguments)")]
        public string method => _callable.Method?.ToString() ?? "";

        [RqlDescription("connect flags (Godot's ConnectFlags: 1 deferred, 2 persistent, 4 one-shot, 8 reference-counted)")]
        public long flags { get; }

        [RqlDescription("the method runs at the end of the frame")]
        public bool deferred => (flags & (long)GodotObject.ConnectFlags.Deferred) != 0;

        [RqlDescription("the connection is removed after the first emission")]
        public bool oneShot => (flags & (long)GodotObject.ConnectFlags.OneShot) != 0;

        [RqlCallable("remove this connection (undoable)")]
        public void Disconnect()
        {
            if (target == null || method.Length == 0) throw new InvalidOperationException("this connection has no target method to disconnect");
            RqlGodot.Disconnect(source, signal, target, method);
        }
    }
}
#endif
