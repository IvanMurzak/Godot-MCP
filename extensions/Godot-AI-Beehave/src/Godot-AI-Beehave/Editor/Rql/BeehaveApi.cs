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
        // The source creates one instance per query: the class table and the scene walk are read once and shared by
        // installed / hint / trees / nodes.
        private BeehaveScripts? _scripts;
        private Node? _root;
        private List<BeehaveNodeRow>? _nodes;

        private BeehaveScripts Scripts => _scripts ??= new BeehaveScripts();

        [RqlDescription("whether the Beehave addon is installed (its BeehaveTree class is registered)")]
        public bool installed => Scripts.Has(BeehaveNames.TreeClass);

        [RqlDescription("how to install the addon when it is missing, else null")]
        public string? hint => installed ? null : BeehaveNames.InstallHint;

        [RqlDescription("every BeehaveTree of the edited scene: name path scriptClass enabled tickRate processThread actor childCount nodeCount node")]
        public List<BeehaveTreeRow> trees
        {
            get
            {
                var all = nodes;
                // One pass: the Beehave nodes below each tree (a nested tree is its own tree, not counted by its parent).
                var counts = new Dictionary<string, int>();
                foreach (var row in all)
                    if (row.tree != null && row.tree != row.path) counts[row.tree] = counts.TryGetValue(row.tree, out var n) ? n + 1 : 1;
                var list = new List<BeehaveTreeRow>();
                foreach (var row in all)
                    if (row.kind == "tree")
                        list.Add(new BeehaveTreeRow(row.node, row.path, row.scriptClass, counts.TryGetValue(row.path, out var n) ? n : 0, _root!));
                return list;
            }
        }

        [RqlDescription("every Beehave node of the edited scene, depth-first (trees, composites, decorators, leaves): name path scriptClass kind tree depth parent childCount node")]
        public List<BeehaveNodeRow> nodes
        {
            get
            {
                if (_nodes != null) return _nodes;
                _nodes = new List<BeehaveNodeRow>();
                _root = EditorInterface.Singleton.GetEditedSceneRoot();
                if (_root != null && installed) Walk(_root, _root, null, 0, _nodes);
                return _nodes;
            }
        }

        // A path is computed only for a Beehave node; its parent's path is the path without the last name (node names
        // cannot contain '/').
        private static void Walk(Node node, Node root, string? tree, int depth, List<BeehaveNodeRow> acc)
        {
            var chain = BeehaveScripts.Chain(node);
            var kind = chain == null ? null : BeehaveNames.KindOf(chain);
            if (kind != null)
            {
                var path = root.GetPathTo(node).ToString();
                if (kind == "tree") { tree = path; depth = 0; }
                var slash = path.LastIndexOf('/');
                acc.Add(new BeehaveNodeRow(node, path, chain![0], kind, tree, depth,
                    node == root ? null : slash < 0 ? "." : path.Substring(0, slash)));
            }
            foreach (var child in node.GetChildren())
                Walk(child, root, tree, tree == null ? 0 : depth + 1, acc);
        }
    }
}
#endif
