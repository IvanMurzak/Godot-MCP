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
    /// <summary>Vector4: compact "(x, y, z, w)" output.</summary>
    public sealed class Godot_Vector4_Adapter : NumericTupleAdapter<Vector4>
    {
        public override string Description => "Vector4: write as \"(x, y, z, w)\" or [x, y, z, w]";
        protected override string Expected => "(x, y, z, w) or [x, y, z, w]";
        protected override int Width => 4;
        protected override float Part(Vector4 v, int index) => 0f + index switch { 0 => v.X, 1 => v.Y, 2 => v.Z, _ => v.W };
        protected override Vector4? Make(float[] f) => f.Length == 4 ? new Vector4(f[0], f[1], f[2], f[3]) : (Vector4?)null;
    }
}
