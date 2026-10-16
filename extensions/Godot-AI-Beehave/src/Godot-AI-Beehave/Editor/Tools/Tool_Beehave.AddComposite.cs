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
        /// Editor-only tool — add a Beehave composite (<c>SelectorComposite</c> or <c>SequenceComposite</c>)
        /// under a tree or another composite. Class-B presence-gated; main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            AddCompositeToolId,
            Title = "Beehave / Add Composite"
        )]
        [Description("Add a Beehave composite node under an existing tree or composite in the edited scene. " +
            "'kind' is 'selector' (runs children until one does NOT fail) or 'sequence' (runs children until " +
            "one fails). 'parentPath' (relative to the scene root) is the node to add it under — typically a " +
            "BeehaveTree or another composite (defaults to the scene root). Optional 'name' renames the node. " +
            "Requires the Beehave addon; returns { Installed: false } with a hint when it is absent.")]
        public BeehaveNodeInfo AddComposite
        (
            [Description("Composite kind: 'selector' or 'sequence'.")]
            string kind,
            [Description("Node path (relative to the edited scene root) of the parent to add the composite " +
                "under (a BeehaveTree or another composite). Defaults to the scene root.")]
            string? parentPath = null,
            [Description("Name for the new composite node. When omitted, Godot's default name is used.")]
            string? name = null
        )
        {
            var className = BeehaveNodeKinds.ResolveCompositeClass(kind); // throws on a bad kind (pure-managed)

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
