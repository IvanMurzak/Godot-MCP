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
using System.Collections.Generic;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// The <c>beehave</c> source: whether the addon is installed and the Beehave nodes of the edited scene as read-only rows
    /// (<c>beehave.trees</c>, <c>beehave.nodes</c>). A row names the node's Beehave class (a GDScript class_name the C#
    /// type cannot show) and holds the node itself in <c>node</c>: writes go through it (<c>get node | set props["…"]</c>),
    /// so they are undoable and mark the scene unsaved. Without the addon the lists are empty and <c>hint</c> says how
    /// to install it. Every RQL query runs on the editor main thread.
    /// </summary>
    [RqlDescription("Beehave behaviour trees: installed, hint, trees, nodes")]
    public sealed class BeehaveApi
    {
        public static readonly BeehaveApi Instance = new BeehaveApi();

        [RqlDescription("whether the Beehave addon is installed (its BeehaveTree class is registered)")]
        public bool installed => new BeehaveScripts().Has(BeehaveNames.TreeClass);

        [RqlDescription("how to install the addon when it is missing, else null")]
        public string? hint => installed ? null : BeehaveNames.InstallHint;

        [RqlDescription("every BeehaveTree of the edited scene: name path scriptClass enabled tickRate processThread actor childCount nodeCount node")]
        public List<BeehaveTreeRow> trees
        {
            get
            {
                var all = nodes;
                var list = new List<BeehaveTreeRow>();
                foreach (var row in all)
                    if (row.kind == "tree")
                        list.Add(new BeehaveTreeRow(row.node, row.path, row.scriptClass, all.FindAll(n => n.tree == row.path && n.node != row.node).Count));
                return list;
            }
        }

        [RqlDescription("every Beehave node of the edited scene, depth-first (trees, composites, decorators, leaves): name path scriptClass kind tree depth parent childCount node")]
        public List<BeehaveNodeRow> nodes
        {
            get
            {
                var list = new List<BeehaveNodeRow>();
                var root = EditorInterface.Singleton.GetEditedSceneRoot();
                var scripts = new BeehaveScripts();
                if (root != null && scripts.Has(BeehaveNames.TreeClass)) Walk(root, root, scripts, null, 0, list);
                return list;
            }
        }

        private static void Walk(Node node, Node root, BeehaveScripts scripts, string? tree, int depth, List<BeehaveNodeRow> acc)
        {
            var chain = scripts.Chain(node);
            var kind = BeehaveNames.KindOf(chain);
            var path = root.GetPathTo(node).ToString();
            if (kind == "tree") { tree = path; depth = 0; }
            if (kind != null) acc.Add(new BeehaveNodeRow(node, path, chain[0], kind, tree, depth, node == root ? null : root.GetPathTo(node.GetParent()).ToString()));
            foreach (var child in node.GetChildren())
                Walk(child, root, scripts, tree, tree == null ? 0 : depth + 1, acc);
        }
    }
}
#endif
