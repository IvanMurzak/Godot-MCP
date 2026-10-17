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

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed: the <c>dialogic</c> source (installed, hint, timelines and characters as read-only rows, the creation
    /// calls, <c>AddText</c>) and the agent guide rendered into the <c>godot-rql</c> skill. Dialogic resources are GDScript
    /// class_names (no C# type, not in ClassDB), so the module creates them itself and adds no adapters. Configuring never
    /// touches Godot, so an absent addon cannot fail it.
    /// </summary>
    public sealed class DialogicModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddSource("dialogic", () => new DialogicApi(),
                    "Dialogic dialogue files of the project: installed, hint, timelines (path name eventCount text; call AddText), " +
                    "characters (path name displayName color description resource; write through resource); calls CreateTimeline, " +
                    "CreateCharacter. Needs the Dialogic addon (dialogic-godot/dialogic 2.0, res://addons/dialogic) enabled; without " +
                    "it the lists are empty and the calls fail")
               .AddGuide(DialogicGuide.Topic, DialogicGuide.Markdown);
    }
}
#endif
