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
    /// <summary>One Beehave node of the edited scene (a row of <c>beehave.nodes</c>); read-only, writes go through <c>node</c>.</summary>
    [RqlDescription("Beehave node (read-only row): name path scriptClass kind tree depth parent childCount node; write through node")]
    public sealed class BeehaveNodeRow
    {
        internal BeehaveNodeRow(Node node, string path, string scriptClass, string kind, string? tree, int depth, string? parent)
        {
            this.node = node;
            this.path = path;
            this.scriptClass = scriptClass;
            this.kind = kind;
            this.tree = tree;
            this.depth = depth;
            this.parent = parent;
        }

        [RqlDisplay, RqlDescription("node name")]
        public string name => node.Name.ToString();

        [RqlDescription("path below the edited scene root (\".\" = the root)")]
        public string path { get; }

        [RqlDescription("the node's script class: a Beehave class (SelectorComposite, ActionLeaf …) or your class_name extending one")]
        public string scriptClass { get; }

        [RqlDescription("tree, composite, decorator, action, condition, leaf, or node (another BeehaveNode)")]
        public string kind { get; }

        [RqlDescription("path of the BeehaveTree this node belongs to, or null outside a tree")]
        public string? tree { get; }

        [RqlDescription("levels below its tree (the tree itself is 0)")]
        public int depth { get; }

        [RqlDescription("path of the parent node, or null for the scene root")]
        public string? parent { get; }

        [RqlDescription("number of child nodes")]
        public int childCount => node.GetChildCount();

        [RqlReference, RqlDescription("the node itself: write through it (get node | set props[\"…\"] = …, call AddChild(…))")]
        public Node node { get; }
    }
}
#endif
