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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    public partial class Tool_Beehave
    {
        /// <summary>
        /// Editor-only tool — create a <c>BeehaveTree</c> behaviour-tree root in the edited scene and return its
        /// structured config. Class-B presence-gated: the FIRST line detects the Beehave addon and returns a
        /// structured <c>Installed = false</c> result when it is absent. All Godot access is marshalled onto the
        /// editor main thread.
        /// </summary>
        [AiTool
        (
            TreeCreateToolId,
            Title = "Beehave / Create Tree"
        )]
        [Description("Create a Beehave 'BeehaveTree' behaviour-tree root in the currently edited Godot scene " +
            "and return its structured config. Optionally pass 'parentPath' (a node path relative to the scene " +
            "root) to parent it (defaults to the scene root — Beehave drives the tree's parent as its actor), " +
            "'name' to rename it, 'actorPath' to set the tree's actor explicitly, 'enabled' (default true), and " +
            "'tickRate' (ticks every N frames, default 1). Requires the third-party Beehave addon to be " +
            "installed; when it is not, returns { Installed: false } with an install hint instead of crashing.")]
        public BeehaveNodeInfo TreeCreate
        (
            [Description("Name for the new BeehaveTree node. When omitted, Godot's default name is used.")]
            string? name = null,
            [Description("Node path (relative to the edited scene root) of the parent. When omitted, the tree " +
                "is parented to the scene root.")]
            string? parentPath = null,
            [Description("Optional node path (relative to the scene root) of the actor the tree drives. When " +
                "omitted, Beehave defaults the actor to the tree's parent.")]
            string? actorPath = null,
            [Description("Whether the tree is enabled (ticks). Defaults to true.")]
            bool enabled = true,
            [Description("Tick rate — the tree ticks once every N frames. Defaults to 1 (every frame).")]
            int tickRate = 1
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!AddonInterop.GlobalClassExists(BeehaveEnums.PresenceClass))
                    return BeehaveNodeInfo.NotInstalled(BeehaveEnums.PresenceClass);

                var root = EditedSceneRootOrThrow();
                var (tree, info) = CreateUnder(BeehaveEnums.TreeClass, parentPath, name);

                tree.Set(BeehaveEnums.EnabledMember, enabled);
                tree.Set(BeehaveEnums.TickRateMember, tickRate < 1 ? 1 : tickRate);
                if (!string.IsNullOrWhiteSpace(actorPath))
                    tree.Set(BeehaveEnums.ActorMember, ResolveOrRoot(actorPath, nameof(actorPath)));

                PopulateTreeScalars(tree, root, info);
                return info;
            });
        }
    }
}
#endif
