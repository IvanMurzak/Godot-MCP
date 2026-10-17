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
    /// <summary>2D node: transform members as C# properties (Position, Rotation, Scale …).</summary>
    public sealed class Godot_Node2D_Adapter : RqlTypeAdapter<Node2D>
    {
        public override string Description => "Node2D: Position, Rotation (radians), RotationDegrees, Scale, Skew, GlobalPosition; vectors as \"(x, y)\"";
    }
}
#endif
