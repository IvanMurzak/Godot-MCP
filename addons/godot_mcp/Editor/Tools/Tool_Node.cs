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
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Node tool family (<c>node-*</c>) — the Godot analog of Unity-MCP's <c>Tool_GameObject</c>. Each
    /// tool method lives in its own partial-class file (Find / Create / Modify / SetParent / Duplicate /
    /// Delete) and drives the Godot editor scene tree via <see cref="EditorInterface"/> through the
    /// main-thread dispatcher.
    ///
    /// <para>
    /// Godot ↔ Unity mapping: <c>Node</c> ↔ <c>GameObject</c>; a node's class name + attached script
    /// ↔ a GameObject's component set; <c>EditorInterface.GetEditedSceneRoot()</c> ↔ the active scene
    /// root. Node identity is resolved from a <see cref="Data.NodeRef"/> (instance id preferred, then
    /// scene-tree path) by <see cref="ResolveNode"/>.
    /// </para>
    ///
    /// Editor-only (<c>#if TOOLS</c>): the handlers touch <see cref="EditorInterface"/> and live
    /// <see cref="Node"/> objects, neither of which exists in a plain (non-editor) build. The pure-managed
    /// pieces (the <see cref="NodeData"/>/<see cref="NodeRef"/> models, validation) live outside this
    /// guard and are unit-tested.
    /// </summary>
    [AiToolType]
    public partial class Tool_Node
    {
        /// <summary>
        /// Resolve a <see cref="Data.NodeRef"/> to a live <see cref="Node"/> in the editor scene tree.
        /// Must be called on the main thread. Returns null and sets <paramref name="error"/> on any
        /// failure (invalid ref, no edited scene, instance id not a Node, path not found).
        ///
        /// Resolution order mirrors <see cref="Data.NodeRef"/>'s declared priority: instance id first
        /// (stable identity), then scene-tree path (which can shift as the tree mutates). A path is
        /// resolved relative to the edited-scene root; a leading '/root/' or '/' is tolerated by
        /// stripping it down to the path under the edited root, and the special path "." (or the root's
        /// own name) resolves to the root itself.
        /// </summary>
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
        internal static NodeData ToNodeData(Node node, int hierarchyDepth = 0)
        {
            var data = new NodeData
            {
                InstanceId = node.GetInstanceId(),
                Name = node.Name.ToString(),
                Path = node.GetPath().ToString(),
                Type = node.GetClass(),
                ScriptResourcePath = GetAttachedScriptPath(node),
                ChildCount = node.GetChildCount(includeInternal: false),
            };

            if (hierarchyDepth > 0)
            {
                data.Children = new System.Collections.Generic.List<NodeData>(data.ChildCount);
                for (int i = 0; i < data.ChildCount; i++)
                {
                    var child = node.GetChild(i, includeInternal: false);
                    if (child != null)
                        data.Children.Add(ToNodeData(child, hierarchyDepth - 1));
                }
            }

            return data;
        }

        /// <summary>res:// path of the script attached to <paramref name="node"/>, or null when none.</summary>
        static string? GetAttachedScriptPath(Node node)
        {
            var scriptVariant = node.GetScript();
            if (scriptVariant.VariantType == Variant.Type.Nil)
                return null;

            var script = scriptVariant.As<Script>();
            var resourcePath = script?.ResourcePath;
            return string.IsNullOrEmpty(resourcePath) ? null : resourcePath;
        }
    }
}
#endif
