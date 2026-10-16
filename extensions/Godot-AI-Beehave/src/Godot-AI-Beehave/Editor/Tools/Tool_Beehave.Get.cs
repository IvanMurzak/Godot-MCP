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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    public partial class Tool_Beehave
    {
        /// <summary>
        /// Editor-only, read-only tool — read a <c>BeehaveTree</c>'s scalar config and dump its node structure
        /// (composites / decorators / leaves with their Beehave class names + depth). Class-B presence-gated;
        /// main-thread-marshalled. Does not modify the scene.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "Beehave / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read a Beehave 'BeehaveTree' node addressed by 'nodePath' (relative to the edited scene " +
            "root): its scalar config (enabled, tick rate, actor, child count) plus a depth-first dump of the " +
            "tree structure (each descendant's name, Beehave class, path, depth). Read-only. Requires the " +
            "Beehave addon; returns { Installed: false } with a hint when it is absent.")]
        public BeehaveNodeInfo Get
        (
            [Description("Node path (relative to the edited scene root) of the BeehaveTree to read.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!AddonInterop.GlobalClassExists(BeehaveEnums.PresenceClass))
                    return BeehaveNodeInfo.NotInstalled(BeehaveEnums.PresenceClass);

                if (string.IsNullOrWhiteSpace(nodePath))
                    throw new ArgumentException("A node path is required.", nameof(nodePath));

                var root = EditedSceneRootOrThrow();
                var tree = ResolveOrRoot(nodePath, nameof(nodePath));

                var info = new BeehaveNodeInfo
                {
                    Installed = true,
                    Addon = AddonGate.AddonName,
                    ClassName = AddonInterop.ScriptClassName(tree) ?? tree.GetClass(),
                    NodePath = root.GetPathTo(tree).ToString()
                };
                PopulateTreeScalars(tree, root, info);
                WalkChildren(tree, root, info.Children, depth: 1);
                return info;
            });
        }
    }
}
#endif
