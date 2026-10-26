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
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Node: <c>name</c> (writable), <c>path</c> below the edited scene root, <c>parent</c> / <c>Owner</c> as references,
    /// <c>children</c> (internal children excluded), <c>index</c>, <c>script</c>; structural calls AddChild,
    /// InstantiateScene, Duplicate, Free, Reparent, MoveChild (undoable, see <see cref="RqlGodot"/>.Nodes); groups, signals and
    /// persistent signal connections with Connect / Disconnect / AddToGroup / RemoveFromGroup (see RqlGodot.Signals).
    /// </summary>
    public sealed class Godot_Node_Adapter : RqlTypeAdapter<Node>
    {
        public override string Description =>
            "Node: name, path, class, parent, children, index, script, groups, signals, connections, incoming, props[\"…\"]; calls " +
            "AddChild(class, name), InstantiateScene(path), Duplicate(), Free(), Reparent(node), MoveChild(index), " +
            "Connect(signal, target, method), Disconnect(signal, target, method), AddToGroup(group), RemoveFromGroup(group); " +
            "a node path or name works where a node is expected";

        protected override void Members(MemberTable<Node> m)
        {
            // The C# Name is a StringName; the virtual name below reads and writes it as text.
            m.Hide("Name");
            m.Add<string>("name", n => n.Name.ToString(), (n, v) => n.Name = v,
                    validate: (n, v) => v == n.Name.ToString() ? null : NodeNameRules.Problem(v, name => SiblingHas(n, name)), description: "node name")
             .Add<string>("path", n => RqlGodot.PathOf(n), description: "path below the edited scene root (\".\" = the root)")
             .Add<Node?>("parent", n => n == RqlGodot.EditedRoot() ? null : n.GetParent(),
                    description: "parent node, null for the scene root (read-only: move a node with call Reparent(node))")
             .Add<List<Node>>("children", n => Children(n), description: "direct child nodes (internal children excluded)", owning: true)
             .Add<int>("childCount", n => n.GetChildCount(includeInternal: false), description: "number of child nodes")
             .Add<int>("index", n => Math.Max(0, n.GetIndex(includeInternal: false)), description: "position among its siblings (reorder with call MoveChild(i))")
             .Add<string?>("script", n => ScriptPath(n), description: "res:// path of the attached script, or null")
             .Add<List<string>>("groups", n => RqlGodot.GroupsOf(n), (n, v) => RqlGodot.SetGroups(n, v ?? new List<string>()),
                    description: "persistent groups the node is in (saved with the scene); assign a list to replace them, or call AddToGroup / RemoveFromGroup")
             .Add<List<RqlSignal>>("signals", n => RqlGodot.SignalsOf(n), description: "signals the node can emit: name args connections")
             .Add<List<RqlConnection>>("connections", n => RqlGodot.ConnectionsOf(n),
                    description: "persistent connections from its signals (saved with the scene): signal target method flags; call Disconnect() on one")
             .Add<List<RqlConnection>>("incoming", n => RqlGodot.IncomingConnectionsOf(n),
                    description: "persistent connections whose method is on this node: source signal method");
            m.Reference("parent", "Owner");
            m.ExplicitOnly("signals", "connections", "incoming");
            m.Hide("Multiplayer");
            m.Display("name");

            m.Untyped
                .AddMethod("AddChild", new[] { new CallableParameter("class", typeof(string)), new CallableParameter("name", typeof(string), hasDefault: true, defaultValue: null) },
                    typeof(Node), (o, a) => RqlGodot.AddChild((Node)o, (string)a[0]!, a[1] as string),
                    "add a new child node of a Godot class (e.g. \"Node3D\", \"Sprite2D\", a GDScript class_name) and return it")
                .AddMethod("InstantiateScene", new[] { new CallableParameter("path", typeof(string)), new CallableParameter("name", typeof(string), hasDefault: true, defaultValue: null) },
                    typeof(Node), (o, a) => RqlGodot.InstantiateScene((Node)o, (string)a[0]!, a[1] as string),
                    "instance the scene at a res:// path as a new child and return it")
                .AddMethod("Duplicate", Array.Empty<CallableParameter>(), typeof(Node), (o, a) => RqlGodot.Duplicate((Node)o),
                    "copy this node (with its children) next to it and return the copy")
                .AddMethod("Free", Array.Empty<CallableParameter>(), typeof(void), (o, a) => { RqlGodot.Remove((Node)o); return null; },
                    "remove this node from the scene (undoable, like Delete in the Scene dock)")
                .AddMethod("Reparent", new[] { new CallableParameter("parent", typeof(Node)), new CallableParameter("keepGlobalTransform", typeof(bool), hasDefault: true, defaultValue: true) },
                    typeof(void), (o, a) => { RqlGodot.Reparent((Node)o, a[0] as Node, a[1] is bool keep ? keep : true); return null; },
                    "move this node under another node, keeping its global transform unless keepGlobalTransform is false")
                .AddMethod("MoveChild", new[] { new CallableParameter("index", typeof(int)) },
                    typeof(int), (o, a) => RqlGodot.MoveChild((Node)o, (int)a[0]!),
                    "move this node to a position among its siblings (0 = first, -1 = last); returns the resulting index")
                .AddMethod("Connect", new[]
                    {
                        new CallableParameter("signal", typeof(string)), new CallableParameter("target", typeof(Node)), new CallableParameter("method", typeof(string)),
                        new CallableParameter("deferred", typeof(bool), hasDefault: true, defaultValue: false),
                        new CallableParameter("oneShot", typeof(bool), hasDefault: true, defaultValue: false),
                    },
                    typeof(RqlConnection),
                    (o, a) => RqlGodot.Connect((Node)o, (string)a[0]!, (Node)a[1]!, (string)a[2]!, a[3] is true, a[4] is true),
                    "connect one of this node's signals to a method of a target node (persistent: saved with the scene, like the Node dock); the method must exist in the target's script")
                .AddMethod("Disconnect", new[] { new CallableParameter("signal", typeof(string)), new CallableParameter("target", typeof(Node)), new CallableParameter("method", typeof(string)) },
                    typeof(void), (o, a) => { RqlGodot.Disconnect((Node)o, (string)a[0]!, (Node)a[1]!, (string)a[2]!); return null; },
                    "remove a connection from one of this node's signals")
                .AddMethod("AddToGroup", new[] { new CallableParameter("group", typeof(string)) },
                    typeof(bool), (o, a) => RqlGodot.AddToGroup((Node)o, (string)a[0]!),
                    "add the node to a persistent group (saved with the scene); returns false when it already was in it")
                .AddMethod("RemoveFromGroup", new[] { new CallableParameter("group", typeof(string)) },
                    typeof(bool), (o, a) => RqlGodot.RemoveFromGroup((Node)o, (string)a[0]!),
                    "remove the node from a group; returns false when it was not in it");
        }

        private static List<Node> Children(Node n)
        {
            var count = n.GetChildCount(includeInternal: false);
            var list = new List<Node>(count);
            for (int i = 0; i < count; i++)
                if (n.GetChild(i, includeInternal: false) is { } child) list.Add(child);
            return list;
        }

        // The root has no siblings in the scene (its parent is the editor's viewport).
        private static bool SiblingHas(Node n, string name) =>
            n != RqlGodot.EditedRoot() && n.GetParent()?.GetNodeOrNull(name) is { } other && other != n;

        private static string? ScriptPath(Node n)
        {
            var script = n.GetScript();
            if (script.VariantType == Variant.Type.Nil) return null;
            var path = script.As<Script>()?.ResourcePath;
            return string.IsNullOrEmpty(path) ? null : path;
        }
    }
}
#endif
