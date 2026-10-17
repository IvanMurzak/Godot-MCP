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
using System;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>Vector4I: compact "(x, y, z, w)" output; whole numbers only.</summary>
    public sealed class Godot_Vector4I_Adapter : NumericTupleAdapter<Vector4I>
    {
        public override string Description => "Vector4I: write as \"(x, y, z, w)\" or [x, y, z, w] with whole numbers";
        protected override string Expected => "(x, y, z, w) or [x, y, z, w] with whole numbers";
        protected override int Width => 4;
        protected override float Part(Vector4I v, int index) => index switch { 0 => v.X, 1 => v.Y, 2 => v.Z, _ => v.W };
        protected override float Tolerance => 0;

        protected override Vector4I? Make(float[] f)
        {
            if (f.Length != 4) return null;
            var i = new int[4];
            for (int k = 0; k < 4; k++)
            {
                if (Math.Abs(f[k] - Math.Round(f[k])) > 1e-6) return null;
                i[k] = (int)Math.Round(f[k]);
            }
            return new Vector4I(i[0], i[1], i[2], i[3]);
        }
    }
}
