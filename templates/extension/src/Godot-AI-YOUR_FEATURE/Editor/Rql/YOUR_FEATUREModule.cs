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

namespace com.IvanMurzak.Godot.MCP.YOUR_FEATURE
{
    /// <summary>
    /// The extension's RQL module. The core addon discovers every public <see cref="IRqlModule"/> in the loaded assemblies,
    /// so this one is active as soon as the package's sources compile into the consumer's Godot project — no registry edit.
    /// What it contributes appears in the core <c>rql-query</c> / <c>rql-mutate</c> / <c>rql-schema</c> tools and its guide
    /// in the <c>godot-rql</c> skill; the extension needs no MCP tools of its own.
    /// <para>
    /// Typical contributions (see the Godot-AI-* extensions): <c>ctx.AddAdapter(new Godot_&lt;Type&gt;_Adapter())</c> for a
    /// Godot type (validated members that replace C# properties, virtual members, <c>Method</c> / <c>Function</c> calls,
    /// <c>Expose("Name")</c> for an existing C# method), <c>ctx.AddSource</c> for state no node owns, and
    /// <c>ctx.AddGuide</c>. Writes are undoable when their target is the Godot object that changes (the core snapshots the
    /// stored properties of every changed node / resource), so put a method on the object it modifies.
    /// </para>
    /// </summary>
    public sealed class YOUR_FEATUREModule : IRqlModule
    {
        /// <summary>Configuration order: the core uses 0 and 10, extensions 100.</summary>
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddSource("YOUR_SOURCE", () => YOUR_FEATUREApi.Instance,
                    "YOUR_DISPLAY_NAME: godotVersion, editorScale; call Echo(message)")
               .AddGuide(YOUR_FEATUREGuide.Topic, YOUR_FEATUREGuide.Markdown);
    }
}
#endif
