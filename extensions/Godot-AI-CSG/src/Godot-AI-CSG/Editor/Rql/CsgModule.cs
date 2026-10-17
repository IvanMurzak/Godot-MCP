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

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed: validated dimensions on the CSG primitives, <c>rootShape</c> on every CSG shape, and the agent guide
    /// rendered into the <c>godot-rql</c> skill. Creating shapes, nesting them under a combiner and setting the boolean
    /// operation are ordinary RQL (the core <c>AddChild</c> call, <c>set Operation = "Subtraction"</c>), so the extension has
    /// no MCP tools of its own.
    /// </summary>
    public sealed class CsgModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddAdapter(new Godot_CsgShape3D_Adapter())
               .AddAdapter(new Godot_CsgBox3D_Adapter())
               .AddAdapter(new Godot_CsgSphere3D_Adapter())
               .AddAdapter(new Godot_CsgCylinder3D_Adapter())
               .AddAdapter(new Godot_CsgTorus3D_Adapter())
               .AddGuide(CsgGuide.Topic, CsgGuide.Markdown);
    }
}
#endif
