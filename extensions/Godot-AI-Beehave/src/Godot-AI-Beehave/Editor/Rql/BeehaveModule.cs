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
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed: the <c>beehave</c> source (installed, hint, trees, nodes as read-only rows) and the agent guide rendered
    /// into the <c>godot-rql</c> skill. Beehave classes are GDScript class_names (no C# type), so the module adds no
    /// adapters: trees are created with the core <c>AddChild("BeehaveTree")</c> and changed through the row's <c>node</c>.
    /// Configuring never touches Godot, so an absent addon cannot fail it.
    /// </summary>
    public sealed class BeehaveModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddSource("beehave", () => BeehaveApi.Instance,
                    "Beehave behaviour trees of the edited scene: installed, hint, trees (name path scriptClass enabled tickRate " +
                    "processThread actor node), nodes (name path scriptClass kind tree depth node); write through node. Needs the " +
                    "Beehave addon (bitbrain/beehave, res://addons/beehave) enabled; without it the lists are empty")
               .AddGuide(BeehaveGuide.Topic, BeehaveGuide.Markdown);
    }
}
#endif
