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

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed: cell rows and the cell calls on TileMapLayer, and the agent guide rendered into the <c>godot-rql</c> skill.
    /// Creating a layer and assigning its TileSet are ordinary RQL (the core <c>AddChild</c> call,
    /// <c>set TileSet = "res://…"</c>), so the extension has no MCP tools of its own.
    /// </summary>
    public sealed class TilemapModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddAdapter(new Godot_TileMapLayer_Adapter())
               .AddGuide(TilemapGuide.Topic, TilemapGuide.Markdown);
    }
}
#endif
