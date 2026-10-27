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
    /// <summary>Vector2I (tile coordinates, sizes): compact "(x, y)" output; whole numbers only.</summary>
    public sealed class Godot_Vector2I_Adapter : NumericTupleAdapter<Vector2I>
    {
        public override string Description => "Vector2I: write as \"(x, y)\" or [x, y] with whole numbers";
        protected override string Expected => "(x, y) or [x, y] with whole numbers";
        protected override int Width => 2;
        protected override float Part(Vector2I v, int index) => index switch { 0 => v.X, _ => v.Y };
        protected override float Tolerance => 0;

        protected override Vector2I? Make(float[] f) => WholeNumbers.Of(f, 2) is { } i ? new Vector2I(i[0], i[1]) : (Vector2I?)null;
    }
}
#endif
