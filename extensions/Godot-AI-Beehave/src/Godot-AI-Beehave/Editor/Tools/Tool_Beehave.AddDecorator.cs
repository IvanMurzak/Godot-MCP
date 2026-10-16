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
        /// Editor-only tool — add a Beehave decorator (<c>InverterDecorator</c> or <c>LimiterDecorator</c>)
        /// under a tree or composite. A decorator wraps a single child and transforms its result. Class-B
        /// presence-gated; main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            AddDecoratorToolId,
            Title = "Beehave / Add Decorator"
        )]
        [Description("Add a Beehave decorator node under an existing tree or composite in the edited scene. " +
            "'kind' is 'inverter' (flips its child's SUCCESS/FAILURE) or 'limiter' (caps how often its child " +
            "may run). 'parentPath' (relative to the scene root) is the node to add it under (defaults to the " +
            "scene root). A decorator is expected to have exactly one child — add a composite or leaf under it " +
            "next. Optional 'name' renames the node. Requires the Beehave addon; returns { Installed: false } " +
            "with a hint when it is absent.")]
        public BeehaveNodeInfo AddDecorator
        (
            [Description("Decorator kind: 'inverter' or 'limiter'.")]
            string kind,
            [Description("Node path (relative to the edited scene root) of the parent to add the decorator " +
                "under (a BeehaveTree or composite). Defaults to the scene root.")]
            string? parentPath = null,
            [Description("Name for the new decorator node. When omitted, Godot's default name is used.")]
            string? name = null
        )
        {
            var className = BeehaveNodeKinds.ResolveDecoratorClass(kind); // throws on a bad kind (pure-managed)

            return MainThread.Instance.Run(() =>
            {
                if (!AddonInterop.GlobalClassExists(BeehaveEnums.PresenceClass))
                    return BeehaveNodeInfo.NotInstalled(BeehaveEnums.PresenceClass);

                var (_, info) = CreateUnder(className, parentPath, name);
                return info;
            });
        }
    }
}
#endif
