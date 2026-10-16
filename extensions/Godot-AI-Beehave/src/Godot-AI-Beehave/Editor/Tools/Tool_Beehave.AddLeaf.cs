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
        /// Editor-only tool — add a Beehave leaf PLACEHOLDER (<c>ActionLeaf</c> or <c>ConditionLeaf</c>) under a
        /// composite or decorator. Beehave leaves are abstract: this tool scaffolds the base node only; the user
        /// attaches a GDScript implementing <c>tick(actor, blackboard)</c>. Class-B presence-gated;
        /// main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            AddLeafToolId,
            Title = "Beehave / Add Leaf"
        )]
        [Description("Add a Beehave leaf PLACEHOLDER under a composite or decorator in the edited scene. " +
            "'kind' is 'action' (an ActionLeaf — does work) or 'condition' (a ConditionLeaf — gates a branch). " +
            "'parentPath' (relative to the scene root) is the composite/decorator to add it under (defaults to " +
            "the scene root). Beehave leaves are ABSTRACT: this creates the base node as a scaffold — attach " +
            "your own GDScript to implement tick(actor, blackboard) -> SUCCESS|RUNNING|FAILURE. Optional 'name' " +
            "renames the node. Requires the Beehave addon; returns { Installed: false } with a hint when absent.")]
        public BeehaveNodeInfo AddLeaf
        (
            [Description("Leaf kind: 'action' or 'condition'.")]
            string kind,
            [Description("Node path (relative to the edited scene root) of the parent composite/decorator to " +
                "add the leaf under. Defaults to the scene root.")]
            string? parentPath = null,
            [Description("Name for the new leaf node. When omitted, Godot's default name is used.")]
            string? name = null
        )
        {
            var className = BeehaveNodeKinds.ResolveLeafClass(kind); // throws on a bad kind (pure-managed)

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
