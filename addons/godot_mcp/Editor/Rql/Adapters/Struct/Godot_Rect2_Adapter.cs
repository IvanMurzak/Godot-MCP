/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>Rect2: compact "(x, y, width, height)" output.</summary>
    public sealed class Godot_Rect2_Adapter : NumericTupleAdapter<Rect2>
    {
        public override string Description => "Rect2: write as \"(x, y, width, height)\" or [x, y, width, height]";
        protected override string Expected => "(x, y, width, height)";
        protected override int Width => 4;
        protected override float Part(Rect2 v, int index) => 0f + index switch { 0 => v.Position.X, 1 => v.Position.Y, 2 => v.Size.X, _ => v.Size.Y };
        protected override Rect2? Make(float[] f) => f.Length == 4 ? new Rect2(f[0], f[1], f[2], f[3]) : (Rect2?)null;
    }
}
