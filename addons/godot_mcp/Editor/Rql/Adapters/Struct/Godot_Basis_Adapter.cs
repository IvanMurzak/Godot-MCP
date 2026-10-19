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
    /// <summary>Basis (3x3 rotation / scale): the three axis vectors as one 9-number tuple.</summary>
    public sealed class Godot_Basis_Adapter : GodotFloatTupleAdapter<Basis>
    {
        public override string Description => "Basis: write as \"(xx, xy, xz, yx, yy, yz, zx, zy, zz)\" (the X, Y, Z axis vectors)";
        protected override string Expected => "(xx, xy, xz, yx, yy, yz, zx, zy, zz)";
        protected override int Width => 9;
        protected override float Component(Basis v, int index) => index switch { 0 => v.X.X, 1 => v.X.Y, 2 => v.X.Z, 3 => v.Y.X, 4 => v.Y.Y, 5 => v.Y.Z, 6 => v.Z.X, 7 => v.Z.Y, _ => v.Z.Z };
        protected override Basis? Make(float[] f) => f.Length == 9 ? new Basis(new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5]), new Vector3(f[6], f[7], f[8])) : (Basis?)null;
    }
}
#endif
