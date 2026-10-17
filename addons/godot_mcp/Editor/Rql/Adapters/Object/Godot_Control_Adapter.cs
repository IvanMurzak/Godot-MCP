/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>UI node: layout members as C# properties; theme overrides through props.</summary>
    public sealed class Godot_Control_Adapter : RqlTypeAdapter<Control>
    {
        public override string Description => "Control: Position, Size, AnchorLeft/Top/Right/Bottom, OffsetLeft/Top/Right/Bottom, SizeFlagsHorizontal, Visible; theme overrides in props[\"theme_override_colors/font_color\"] …";
    }
}
#endif
