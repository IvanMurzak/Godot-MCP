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

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// The <c>phantomcamera</c> source: whether the addon is installed and its nodes in the edited scene as read-only rows
    /// (<c>phantomcamera.cameras</c>, <c>phantomcamera.hosts</c>). A row names the node's Phantom Camera class (a GDScript
    /// class_name the C# type cannot show), shows the modes by name and holds the node itself in <c>node</c>: writes go
    /// through it (<c>get node | set props["…"]</c>, undoable), plus the validated <c>SetFollow</c> / <c>SetLookAt</c>
    /// calls on a camera row. Without the addon the lists are empty and <c>hint</c> says how to install it.
    /// </summary>
    [RqlDescription("Phantom Camera: installed, hint, cameras, hosts")]
    public sealed class PhantomCameraApi
    {
        public static readonly PhantomCameraApi Instance = new PhantomCameraApi();

        [RqlDescription("whether the Phantom Camera addon is installed (its PhantomCamera3D class is registered)")]
        public bool installed => new PhantomCameraScripts().Has(PhantomCameraNames.PhantomCamera3D);

        [RqlDescription("how to install the addon when it is missing, else null")]
        public string? hint => installed ? null : PhantomCameraNames.InstallHint;

        [RqlDescription("every PhantomCamera3D / PhantomCamera2D of the edited scene, depth-first: name path scriptClass priority followMode followTarget lookAtMode lookAtTarget followDamping lookAtDamping node; calls SetFollow, SetLookAt")]
        public List<PhantomCameraRow> cameras
        {
            get
            {
                var list = new List<PhantomCameraRow>();
                Walk((node, root, chain) =>
                {
                    var is3D = chain.Contains(PhantomCameraNames.PhantomCamera3D);
                    if (is3D || chain.Contains(PhantomCameraNames.PhantomCamera2D))
                        list.Add(new PhantomCameraRow(node, root.GetPathTo(node).ToString(), chain[0], !is3D));
                });
                return list;
            }
        }

        [RqlDescription("every PhantomCameraHost of the edited scene: name path camera (the Camera3D / Camera2D it drives) node")]
        public List<PhantomCameraHostRow> hosts
        {
            get
            {
                var list = new List<PhantomCameraHostRow>();
                Walk((node, root, chain) =>
                {
                    if (chain.Contains(PhantomCameraNames.PhantomCameraHost))
                        list.Add(new PhantomCameraHostRow(node, root.GetPathTo(node).ToString(), chain[0]));
                });
                return list;
            }
        }

        private static void Walk(System.Action<Node, Node, List<string>> visit)
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            var scripts = new PhantomCameraScripts();
            if (root == null || !scripts.Has(PhantomCameraNames.PhantomCamera3D)) return;
            var stack = new Stack<Node>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                var chain = scripts.Chain(node);
                if (chain.Count > 0) visit(node, root, chain);
                var children = node.GetChildren();
                for (int i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
            }
        }

        /// <summary>The path of a node below the edited scene root, or null when it is not in the edited scene.</summary>
        internal static string? PathInScene(Node? node)
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (node == null || root == null || !GodotObject.IsInstanceValid(node)) return null;
            return node == root || root.IsAncestorOf(node) ? root.GetPathTo(node).ToString() : null;
        }
    }
}
#endif
