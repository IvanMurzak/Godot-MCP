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

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed. Terrain3D is a GDExtension class without a C# type (its nodes are <c>Node3D</c> wrappers), so the module adds
    /// sources and a function instead of adapters: <c>terrain3d</c> (one view per Terrain3D node, with <c>ChangeRegionSize</c>),
    /// <c>terrain3d.addon</c> and <c>terrain3d.newMaterial()</c>, plus the agent guide rendered into the <c>godot-rql</c> skill.
    /// Creating a terrain is the core <c>AddChild("Terrain3D", "Terrain")</c>; its properties are written on the node itself
    /// (<c>props["…"]</c>), so those writes are undoable. The extension has no MCP tools of its own.
    /// </summary>
    public sealed class Terrain3DModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddSource("terrain3d", () => Terrain3DApi.Terrains(),
                    "Terrain3D nodes of the edited scene (Terrain3D extension): node name path regionSize dataDirectory material regionCount " +
                    "meshLods meshSize vertexSpacing version; call ChangeRegionSize(size)")
               .AddSource("terrain3d.addon", () => Terrain3DApi.Addon, "is the Terrain3D GDExtension installed: installed addon hint")
               .AddFunction("terrain3d.newMaterial", Terrain3DApi.NewMaterial,
                    "terrain3d.newMaterial(): a new Terrain3DMaterial, e.g. terrain3d | get node | set props[\"material\"] = terrain3d.newMaterial()")
               .AddGuide(Terrain3DGuide.Topic, Terrain3DGuide.Markdown);
    }
}
#endif
