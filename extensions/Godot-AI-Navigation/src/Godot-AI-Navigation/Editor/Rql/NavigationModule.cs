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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed: validated members on the navigation agents, regions and links (2D and 3D), <c>Bake()</c> and
    /// <c>polygonCount</c> on the regions, outline calls on NavigationPolygon, and the agent guide rendered into the
    /// <c>godot-rql</c> skill. Creating nodes and assigning their resources are ordinary RQL (the core <c>AddChild</c> call,
    /// <c>set NavigationMesh = {"$type": "NavigationMesh"}</c>), so the extension has no MCP tools of its own.
    /// </summary>
    public sealed class NavigationModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddAdapter(new Godot_NavigationRegion3D_Adapter())
               .AddAdapter(new Godot_NavigationRegion2D_Adapter())
               .AddAdapter(new Godot_NavigationAgent3D_Adapter())
               .AddAdapter(new Godot_NavigationAgent2D_Adapter())
               .AddAdapter(new Godot_NavigationLink3D_Adapter())
               .AddAdapter(new Godot_NavigationLink2D_Adapter())
               .AddAdapter(new Godot_NavigationPolygon_Adapter())
               .AddGuide(NavigationGuide.Topic, NavigationGuide.Markdown);
    }
}
#endif
