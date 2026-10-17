/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// One BeehaveTree of the edited scene (a row of <c>beehave.trees</c>): its settings, read live from the node.
    /// Read-only; change them through <c>node</c> (<c>get node | set props["tick_rate"] = 2</c>), which is undoable.
    /// </summary>
    [RqlDescription("BeehaveTree (read-only row): name path scriptClass enabled tickRate processThread actor childCount nodeCount node; write through node")]
    public sealed class BeehaveTreeRow
    {
        private readonly Node _root;

        internal BeehaveTreeRow(Node node, string path, string scriptClass, int nodeCount, Node root)
        {
            _root = root;
            this.node = node;
            this.path = path;
            this.scriptClass = scriptClass;
            this.nodeCount = nodeCount;
        }

        [RqlDisplay, RqlDescription("node name")]
        public string name => node.Name.ToString();

        [RqlDescription("path below the edited scene root (\".\" = the root)")]
        public string path { get; }

        [RqlDescription("BeehaveTree, or your class_name extending it")]
        public string scriptClass { get; }

        [RqlDescription("whether the tree ticks (props[\"enabled\"])")]
        public bool enabled => node.Get(BeehaveNames.EnabledProperty).AsBool();

        [RqlDescription("the tree ticks once every N frames (props[\"tick_rate\"], at least 1)")]
        public int tickRate => node.Get(BeehaveNames.TickRateProperty).AsInt32();

        [RqlDescription("Idle, Physics or Manual (props[\"process_thread\"] = 0, 1, 2)")]
        public string processThread => BeehaveNames.ProcessThreadLabel(node.Get(BeehaveNames.ProcessThreadProperty).AsInt32());

        [RqlDescription("path of the actor node the tree drives (props[\"actor\"]; Beehave defaults it to the parent), or null")]
        public string? actor
        {
            get
            {
                var actorNode = node.Get(BeehaveNames.ActorProperty).As<Node>();
                return actorNode == null || !GodotObject.IsInstanceValid(actorNode) || !(actorNode == _root || _root.IsAncestorOf(actorNode))
                    ? null : _root.GetPathTo(actorNode).ToString();
            }
        }

        [RqlDescription("number of direct child nodes")]
        public int childCount => node.GetChildCount();

        [RqlDescription("number of Beehave nodes below the tree")]
        public int nodeCount { get; }

        [RqlReference, RqlDescription("the BeehaveTree node: write through it (get node | set props[\"tick_rate\"] = 2)")]
        public Node node { get; }
    }
}
#endif
