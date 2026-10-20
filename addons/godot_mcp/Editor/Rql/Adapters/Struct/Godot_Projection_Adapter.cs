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

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Projection (a 4x4 matrix, e.g. a <c>mat4</c> shader uniform): 16 numbers column by column — the x, y, z and w columns,
    /// each (x, y, z, w) — the order Godot writes <c>Projection(…)</c> in .tres files.
    /// </summary>
    public sealed class Godot_Projection_Adapter : GodotFloatTupleAdapter<Projection>
    {
        public override string Description => "Projection (4x4 matrix): write as 16 numbers column by column, \"(xx, xy, xz, xw, yx, …, ww)\" or a list";
        protected override string Expected => "16 numbers, column by column: (xx, xy, xz, xw, yx, yy, yz, yw, zx, zy, zz, zw, wx, wy, wz, ww)";
        protected override int Width => 16;

        protected override float Component(Projection v, int index)
        {
            var column = (index / 4) switch { 0 => v.X, 1 => v.Y, 2 => v.Z, _ => v.W };
            return (index % 4) switch { 0 => column.X, 1 => column.Y, 2 => column.Z, _ => column.W };
        }

        protected override Projection? Make(float[] f) => f.Length == 16
            ? new Projection(new Vector4(f[0], f[1], f[2], f[3]), new Vector4(f[4], f[5], f[6], f[7]),
                new Vector4(f[8], f[9], f[10], f[11]), new Vector4(f[12], f[13], f[14], f[15]))
            : (Projection?)null;
    }
}
#endif
