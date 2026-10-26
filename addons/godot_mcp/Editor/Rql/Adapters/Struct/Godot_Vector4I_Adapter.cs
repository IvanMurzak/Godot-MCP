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
    /// <summary>Vector4I: compact "(x, y, z, w)" output; whole numbers only.</summary>
    public sealed class Godot_Vector4I_Adapter : GodotTupleAdapter<Vector4I>
    {
        public override string Description => "Vector4I: write as \"(x, y, z, w)\" or [x, y, z, w] with whole numbers";
        protected override string Expected => "(x, y, z, w) or [x, y, z, w] with whole numbers";
        protected override int Width => 4;
        protected override float Part(Vector4I v, int index) => index switch { 0 => v.X, 1 => v.Y, 2 => v.Z, _ => v.W };
        protected override float Tolerance => 0;

        protected override Vector4I? Make(float[] f) => WholeNumbers.Of(f, 4) is { } i ? new Vector4I(i[0], i[1], i[2], i[3]) : (Vector4I?)null;
    }
}
#endif
