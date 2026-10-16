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
using System;
using System.Collections.Generic;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// Editor-only shared helpers for the <c>beehave-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>s, and drive the Beehave addon dynamically via
    /// <see cref="AddonInterop"/>). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate, AFTER the caller's presence gate has confirmed the addon is
    /// installed — so the dynamic instantiate/Set/Get/Call never run against a missing class.
    /// </summary>
    public partial class Tool_Beehave
    {
        /// <summary>The edited scene root, or throw a clear error when no scene is open.</summary>
        static Node EditedSceneRootOrThrow()
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
                throw new InvalidOperationException(
                    "No scene is currently being edited; open or create a scene first.");
            return root;
        }

        /// <summary>
        /// Resolve <paramref name="path"/> (relative to the edited scene root) to a node. An empty/whitespace
        /// path resolves to the scene root itself. Throws when a non-empty path matches no node.
        /// </summary>
        static Node ResolveOrRoot(string? path, string argName)
        {
            var root = EditedSceneRootOrThrow();
            if (string.IsNullOrWhiteSpace(path))
                return root;
            return root.GetNodeOrNull(new NodePath(path))
                ?? throw new ArgumentException($"No node found at path '{path}'.", argName);
        }

        /// <summary>
        /// Instantiate the Beehave node <paramref name="className"/> by string, parent it under
        /// <paramref name="parentPath"/> (default: scene root), set its <paramref name="name"/> (optional),
        /// own it by the scene root (so it saves), select it in the editor, and return a populated
        /// <see cref="BeehaveNodeInfo"/> with <c>Installed = true</c> and the deterministic
        /// <see cref="BeehaveNodeInfo.ClassName"/> (= the class we created, not <c>node.GetClass()</c>).
        /// </summary>
        static (Node node, BeehaveNodeInfo info) CreateUnder(string className, string? parentPath, string? name)
        {
            var root = EditedSceneRootOrThrow();
            var parent = ResolveOrRoot(parentPath, nameof(parentPath));

            var node = AddonInterop.InstantiateScriptNode(className)
                ?? throw new InvalidOperationException(
                    $"Beehave class '{className}' could not be instantiated even though the addon is installed.");

            if (!string.IsNullOrWhiteSpace(name))
                node.Name = name;

            parent.AddChild(node);
            node.Owner = root; // persist with the scene

            EditorInterface.Singleton.MarkSceneAsUnsaved();
            EditorInterface.Singleton.EditNode(node);

            var info = new BeehaveNodeInfo
            {
                Installed = true,
                Addon = AddonGate.AddonName,
                ClassName = className,
                NodePath = root.GetPathTo(node).ToString(),
                ParentPath = root == parent ? string.Empty : root.GetPathTo(parent).ToString()
            };
            return (node, info);
        }

        /// <summary>Read the BeehaveTree scalar config (enabled / tick_rate / actor / child count) into the result.</summary>
        static void PopulateTreeScalars(Node tree, Node root, BeehaveNodeInfo info)
        {
            info.Enabled = tree.Get(BeehaveEnums.EnabledMember).AsBool();
            info.TickRate = tree.Get(BeehaveEnums.TickRateMember).AsInt32();

            var actor = tree.Get(BeehaveEnums.ActorMember).As<Node>();
            info.ActorPath = (actor != null && actor.IsInsideTree()) ? root.GetPathTo(actor).ToString() : string.Empty;

            info.ChildCount = tree.GetChildCount(); // authored children (internal blackboard is INTERNAL_MODE)
        }

        /// <summary>Depth-first dump of the Beehave structure under <paramref name="node"/> into <paramref name="acc"/>.</summary>
        static void WalkChildren(Node node, Node root, List<BeehaveChildInfo> acc, int depth)
        {
            foreach (var child in node.GetChildren())
            {
                acc.Add(new BeehaveChildInfo
                {
                    Name = child.Name,
                    ClassName = AddonInterop.ScriptClassName(child) ?? child.GetClass(),
                    NodePath = root.GetPathTo(child).ToString(),
                    Depth = depth
                });
                WalkChildren(child, root, acc, depth + 1);
            }
        }
    }
}
#endif
