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
using com.IvanMurzak.Godot.MCP.Data;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Scene-tree helpers shared by the kept editor tools (<c>script-attach-to-node</c>, <c>screenshot-*</c>) and the
    /// Node reflection converter. The <c>node-*</c> tool family was replaced by RQL (<c>rql-query</c> /
    /// <c>rql-mutate</c> over the <c>nodes</c> source, see docs/rql.md); the class keeps its name so existing call sites
    /// and the reflection-resolver wiring stay unchanged. It declares no tools.
    /// </summary>
    public static partial class Tool_Node
    {
        internal static Node? ResolveNode(Data.NodeRef? nodeRef, out string? error)
        {
            error = null;

            if (nodeRef == null)
            {
                error = "nodeRef is null.";
                return null;
            }
            if (!nodeRef.IsValid(out error))
                return null;

            // 1) Instance id (priority 1).
            if (nodeRef.InstanceId != 0)
            {
                if (!GodotObject.IsInstanceIdValid(nodeRef.InstanceId))
                {
                    error = $"No live object with instanceId '{nodeRef.InstanceId}'.";
                    return null;
                }
                var obj = GodotObject.InstanceFromId(nodeRef.InstanceId);
                if (obj is Node nodeById)
                    return nodeById;

                error = $"Object with instanceId '{nodeRef.InstanceId}' is not a Node (it is '{obj?.GetClass() ?? "null"}').";
                return null;
            }

            // 2) Scene-tree path (priority 2).
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
            {
                error = "No scene is currently being edited; cannot resolve a Node by path.";
                return null;
            }

            var path = NormalizePathToEditedRoot(nodeRef.Path!, root);

            if (string.IsNullOrEmpty(path) || path == ".")
                return root;

            var node = root.GetNodeOrNull(path);
            if (node == null)
            {
                error = $"Node not found at path '{nodeRef.Path}' (resolved relative to edited scene root '{root.Name}').";
                return null;
            }
            return node;
        }

        /// <summary>
        /// Reduce an arbitrary user-supplied path to one resolvable against the edited-scene root.
        /// Accepts absolute forms ('/root/Main/Player'), edited-root-prefixed forms ('Main/Player' where
        /// 'Main' is the root), and relative forms. Delegates the pure-string transform to
        /// <see cref="NodePathNormalizer.Normalize"/> (which is unit-tested off the editor).
        /// </summary>
        internal static string NormalizePathToEditedRoot(string rawPath, Node editedRoot)
            => NodePathNormalizer.Normalize(rawPath, editedRoot.Name.ToString());

        /// <summary>
        /// Build a structured <see cref="NodeData"/> from a live node. When <paramref name="hierarchyDepth"/>
        /// is &gt; 0, recursively populates <see cref="NodeData.Children"/> up to that depth (0 = the node
        /// only, children null). Must be called on the main thread.
        /// </summary>
        /// <summary>
        /// Give every descendant of <paramref name="node"/> without an owner the <paramref name="owner"/> (the edited scene
        /// root) so it is saved with the scene; nodes owned by an instanced sub-scene keep their owner.
        /// </summary>
        internal static void SetOwnerRecursive(Node node, Node owner)
        {
            var count = node.GetChildCount(includeInternal: false);
            for (int i = 0; i < count; i++)
            {
                var child = node.GetChild(i, includeInternal: false);
                if (child == null)
                    continue;
                if (child.Owner == null)
                {
                    child.Owner = owner;
                    SetOwnerRecursive(child, owner);
                }
            }
        }
    }
}
#endif
