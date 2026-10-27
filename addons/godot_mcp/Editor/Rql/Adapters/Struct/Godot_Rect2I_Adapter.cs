/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>Rect2I: compact "(x, y, width, height)" output; whole numbers only.</summary>
    public sealed class Godot_Rect2I_Adapter : GodotIntTupleAdapter<Rect2I>
    {
        public override string Description => "Rect2I: write as \"(x, y, width, height)\" with whole numbers";
        protected override string Expected => "(x, y, width, height) with whole numbers";
        protected override int Width => 4;
        protected override int Whole(Rect2I v, int index) => index switch { 0 => v.Position.X, 1 => v.Position.Y, 2 => v.Size.X, _ => v.Size.Y };

        protected override Rect2I? Make(float[] f) => WholeNumbers.Of(f, 4) is { } i ? new Rect2I(i[0], i[1], i[2], i[3]) : (Rect2I?)null;
    }
}
#endif
