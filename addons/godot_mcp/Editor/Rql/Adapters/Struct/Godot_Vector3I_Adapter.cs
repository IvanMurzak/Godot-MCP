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
    /// <summary>Vector3I (grid cells): compact "(x, y, z)" output; whole numbers only.</summary>
    public sealed class Godot_Vector3I_Adapter : NumericTupleAdapter<Vector3I>
    {
        public override string Description => "Vector3I: write as \"(x, y, z)\" or [x, y, z] with whole numbers";
        protected override string Expected => "(x, y, z) or [x, y, z] with whole numbers";
        protected override int Width => 3;
        protected override float Part(Vector3I v, int index) => index switch { 0 => v.X, 1 => v.Y, _ => v.Z };
        protected override float Tolerance => 0;

        protected override Vector3I? Make(float[] f) => WholeNumbers.Of(f, 3) is { } i ? new Vector3I(i[0], i[1], i[2]) : (Vector3I?)null;
    }
}
#endif
